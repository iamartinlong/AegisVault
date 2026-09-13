using System.Text;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class DeviceKeyTests : IDisposable
{
    private static readonly byte[] Password = "master password"u8.ToArray();
    private static readonly byte[] NewPassword = "new master password"u8.ToArray();

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

    public DeviceKeyTests()
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
        }
    }

    [Fact]
    public void RememberDeviceEnablesPasswordlessUnlock()
    {
        var protector = new FakeKeyProtector();

        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            vault.AddEntry(new PasswordEntry { Title = "GitHub", Password = "s3cret" });
            Assert.False(vault.HasDeviceKey(protector));

            vault.RememberDevice(protector);
            Assert.True(vault.HasDeviceKey(protector));

            vault.Lock();
        }

        using var reopened = VaultService.Open(_vaultPath);
        Assert.True(reopened.TryUnlockWithDeviceKey(protector));
        Assert.True(reopened.IsUnlocked);
        Assert.Single(reopened.Entries);
        Assert.Equal("s3cret", Assert.Single(reopened.Entries).Password);
    }

    [Fact]
    public void ForgetDeviceRemovesKey()
    {
        var protector = new FakeKeyProtector();

        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.RememberDevice(protector);
        vault.ForgetDevice(protector);
        vault.Lock();

        Assert.False(vault.HasDeviceKey(protector));
        Assert.False(vault.TryUnlockWithDeviceKey(protector));
    }

    [Fact]
    public void TryUnlockWithoutRememberedKeyReturnsFalse()
    {
        var protector = new FakeKeyProtector();

        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.Lock();

        Assert.False(vault.TryUnlockWithDeviceKey(protector));
        Assert.False(vault.IsUnlocked);
    }

    [Fact]
    public void MasterPasswordChangeInvalidatesDeviceKey()
    {
        var protector = new FakeKeyProtector();

        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            vault.RememberDevice(protector);
            vault.ChangeMasterPassword(NewPassword);
        }

        using var reopened = VaultService.Open(_vaultPath);
        Assert.False(reopened.HasDeviceKey(protector));
        Assert.False(reopened.TryUnlockWithDeviceKey(protector));
        Assert.Equal(VaultUnlockStatus.Success, reopened.Unlock(NewPassword));
    }

    [Fact]
    public void CorruptDeviceBlobReturnsFalse()
    {
        var protector = new FakeKeyProtector();

        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            vault.RememberDevice(protector);
        }

        using (var connection = new SqliteConnection($"Data Source={_vaultPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE device_keys SET blob = $blob WHERE protector = 'fake';";
            command.Parameters.AddWithValue("$blob", new byte[] { 1, 2, 3 });
            command.ExecuteNonQuery();
        }

        using var reopened = VaultService.Open(_vaultPath);
        Assert.False(reopened.TryUnlockWithDeviceKey(protector));
    }

    [Fact]
    public void UnavailableProtectorIsRejected()
    {
        var protector = new FakeKeyProtector { IsAvailable = false };

        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);

        Assert.False(vault.HasDeviceKey(protector));
        Assert.False(vault.TryUnlockWithDeviceKey(protector));
        Assert.Throws<InvalidOperationException>(() => vault.RememberDevice(protector));
    }

    [Fact]
    public void MigratesV1SchemaAndSupportsDeviceKeys()
    {
        var protector = new FakeKeyProtector();

        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            vault.AddEntry(new PasswordEntry { Title = "GitHub" });
        }

        // Simulate a vault created by the previous schema version.
        using (var connection = new SqliteConnection($"Data Source={_vaultPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE device_keys; PRAGMA user_version = 1;";
            command.ExecuteNonQuery();
        }

        using var reopened = VaultService.Open(_vaultPath);
        Assert.Equal(VaultUnlockStatus.Success, reopened.Unlock(Password));
        Assert.Single(reopened.Entries);

        reopened.RememberDevice(protector);
        Assert.True(reopened.HasDeviceKey(protector));
    }
}
