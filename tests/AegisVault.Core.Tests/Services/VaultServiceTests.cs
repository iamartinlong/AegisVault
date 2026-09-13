using System.Text;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class VaultServiceTests : IDisposable
{
    private static readonly byte[] Password = Encoding.UTF8.GetBytes("correct horse battery staple");
    private static readonly byte[] NewPassword = Encoding.UTF8.GetBytes("a different master password");

    private static readonly VaultOptions FastOptions = new()
    {
        Kdf = new KdfParameters
        {
            Algorithm = KdfParameters.AlgorithmArgon2id,
            Iterations = 3,
            MemoryBytes = 8L * 1024 * 1024,
        },
    };

    private readonly string _directory;
    private readonly string _vaultPath;

    public VaultServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "aegis-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _vaultPath = Path.Combine(_directory, "vault.aegis");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    [Fact]
    public void CreateUnlock_And_EntryRoundTrip()
    {
        Guid entryId;
        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            Assert.True(vault.IsUnlocked);
            var added = vault.AddEntry(TestEntry("GitHub"));
            entryId = added.Id;
        }

        using var reopened = VaultService.Open(_vaultPath);
        Assert.False(reopened.IsUnlocked);
        Assert.Equal(VaultUnlockStatus.Success, reopened.Unlock(Password));
        Assert.True(reopened.IsUnlocked);

        var entry = Assert.Single(reopened.Entries);
        Assert.Equal(entryId, entry.Id);
        AssertEntryEquals(TestEntry("GitHub") with { Id = entryId }, entry);
    }

    [Fact]
    public void WrongPasswordReturnsWrongPasswordStatus()
    {
        using (VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
        }

        using var vault = VaultService.Open(_vaultPath);
        Assert.Equal(VaultUnlockStatus.WrongPassword, vault.Unlock(Encoding.UTF8.GetBytes("wrong")));
        Assert.False(vault.IsUnlocked);
        Assert.Empty(vault.Entries);
    }

    [Fact]
    public void ChangeMasterPasswordReWrapsKeyOnly()
    {
        PasswordEntry original;
        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            original = vault.AddEntry(TestEntry("Mail"));
            vault.ChangeMasterPassword(NewPassword);
        }

        using var reopened = VaultService.Open(_vaultPath);
        Assert.Equal(VaultUnlockStatus.WrongPassword, reopened.Unlock(Password));
        Assert.Equal(VaultUnlockStatus.Success, reopened.Unlock(NewPassword));

        var entry = Assert.Single(reopened.Entries);
        AssertEntryEquals(original, entry);
    }

    [Fact]
    public void UpdateAndDeleteEntry()
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var entry = vault.AddEntry(TestEntry("GitHub"));

        var updated = entry with { Title = "GitHub (work)", Tags = ["work"] };
        Assert.True(vault.UpdateEntry(updated));
        Assert.Equal("GitHub (work)", Assert.Single(vault.Entries).Title);

        Assert.True(vault.DeleteEntry(entry.Id));
        Assert.Empty(vault.Entries);

        Assert.False(vault.UpdateEntry(updated));
        Assert.False(vault.DeleteEntry(entry.Id));
    }

    [Fact]
    public void LockClearsSession()
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.AddEntry(TestEntry("GitHub"));

        vault.Lock();

        Assert.False(vault.IsUnlocked);
        Assert.Empty(vault.Entries);
        Assert.Throws<InvalidOperationException>(() => vault.AddEntry(TestEntry("Blocked")));
    }

    [Fact]
    public void TamperedEntryReportsCorrupted()
    {
        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            vault.AddEntry(TestEntry("GitHub"));
        }

        using (var connection = new SqliteConnection($"Data Source={_vaultPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE entries SET ciphertext = zeroblob(length(ciphertext)) WHERE rowid = 1;";
            command.ExecuteNonQuery();
        }

        using var reopened = VaultService.Open(_vaultPath);
        Assert.Equal(VaultUnlockStatus.Corrupted, reopened.Unlock(Password));
    }

    [Fact]
    public void VaultFileDoesNotContainPlaintextEntryContent()
    {
        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            vault.AddEntry(TestEntry("UniqueTitle-9f7d3e"));
        }

        var bytes = File.ReadAllBytes(_vaultPath);
        Assert.DoesNotContain("UniqueTitle-9f7d3e", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void BackupCreatesUsableCopy()
    {
        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            vault.AddEntry(TestEntry("GitHub"));
            vault.SaveBackup();
        }

        var backupPath = _vaultPath + ".bak";
        Assert.True(File.Exists(backupPath));

        using var backup = VaultService.Open(backupPath);
        Assert.Equal(VaultUnlockStatus.Success, backup.Unlock(Password));
        Assert.Single(backup.Entries);
    }

    [Fact]
    public void CreateOnExistingPathThrows()
    {
        using (VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
        }

        Assert.Throws<InvalidOperationException>(() => VaultService.CreateNew(_vaultPath, Password, FastOptions));
    }

    [Fact]
    public void Pbkdf2VaultCanBeCreatedAndUnlocked()
    {
        var options = new VaultOptions
        {
            Kdf = new KdfParameters
            {
                Algorithm = KdfParameters.AlgorithmPbkdf2Sha256,
                Iterations = 1_000,
            },
        };

        using (var vault = VaultService.CreateNew(_vaultPath, Password, options))
        {
            vault.AddEntry(TestEntry("Fallback"));
        }

        using var reopened = VaultService.Open(_vaultPath);
        Assert.Equal(VaultUnlockStatus.Success, reopened.Unlock(Password));
        Assert.Single(reopened.Entries);
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);

        var aad = Encoding.UTF8.GetBytes("settings:v1");
        var plaintext = Encoding.UTF8.GetBytes("{\"autoLockMinutes\":5}");

        var (nonce, ciphertext, tag) = vault.EncryptSetting(plaintext, aad);
        vault.WriteSetting("user-config", nonce, ciphertext, tag);

        var stored = vault.ReadSetting("user-config");
        Assert.NotNull(stored);

        var decrypted = vault.DecryptSetting(stored!.Value.Nonce, stored.Value.Ciphertext, stored.Value.Tag, aad);
        Assert.Equal(plaintext, decrypted);
    }

    private static PasswordEntry TestEntry(string title) => new()
    {
        Title = title,
        Username = "user@example.com",
        Password = "s3cret!",
        Url = "https://example.com",
        Notes = "some note",
        TotpSecret = "JBSWY3DPEHPK3PXP",
        Tags = ["work", "mail"],
        CustomFields = new Dictionary<string, string> { ["pin"] = "1234" },
    };

    private static void AssertEntryEquals(PasswordEntry expected, PasswordEntry actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Username, actual.Username);
        Assert.Equal(expected.Password, actual.Password);
        Assert.Equal(expected.Url, actual.Url);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.TotpSecret, actual.TotpSecret);
        Assert.Equal(expected.Tags, actual.Tags);
        Assert.Equal(expected.CustomFields, actual.CustomFields);
    }
}
