using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using AegisVault.Core.Storage;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class VaultExportServiceTests
{
    private static readonly byte[] Passphrase = Encoding.UTF8.GetBytes("a portable export passphrase");

    private static readonly VaultOptions FastOptions = new()
    {
        Kdf = new KdfParameters
        {
            Algorithm = KdfParameters.AlgorithmArgon2id,
            Iterations = 3,
            MemoryBytes = 8L * 1024 * 1024,
        },
    };

    [Fact]
    public void EncryptedExportRoundTripsEntriesAndCategories()
    {
        var category = new Category { Name = "Work", Color = "blue" };
        var entry = new PasswordEntry
        {
            Title = "GitHub",
            Username = "octocat",
            Password = "s3cret",
            Urls = ["https://github.com"],
            TotpSecret = "JBSWY3DPEHPK3PXP",
            CustomFields = new Dictionary<string, string> { ["pin"] = "1234" },
            CategoryId = category.Id,
            LastOpenedAt = DateTimeOffset.UtcNow,
        };

        var bytes = VaultExportService.ExportEncrypted([entry], [category], Passphrase, FastOptions);
        var payload = VaultExportService.ImportEncrypted(bytes, Passphrase);

        var loadedEntry = Assert.Single(payload.Entries!);
        Assert.Equal("GitHub", loadedEntry.Title);
        Assert.Equal("octocat", loadedEntry.Username);
        Assert.Equal("s3cret", loadedEntry.Password);
        Assert.Equal("JBSWY3DPEHPK3PXP", loadedEntry.TotpSecret);
        Assert.Equal("1234", loadedEntry.CustomFields["pin"]);
        Assert.Equal(entry.LastOpenedAt, loadedEntry.LastOpenedAt);

        var loadedCategory = Assert.Single(payload.Categories!);
        Assert.Equal("Work", loadedCategory.Name);
        Assert.Equal("blue", loadedCategory.Color);
    }

    [Fact]
    public void EncryptedExportSkipsRecycledEntries()
    {
        var live = new PasswordEntry { Title = "Live" };
        var recycled = new PasswordEntry { Title = "Recycled", DeletedAt = DateTimeOffset.UtcNow };

        var bytes = VaultExportService.ExportEncrypted([live, recycled], [], Passphrase, FastOptions);
        var payload = VaultExportService.ImportEncrypted(bytes, Passphrase);

        Assert.Equal("Live", Assert.Single(payload.Entries!).Title);
    }

    [Fact]
    public void WrongPassphraseFails()
    {
        var bytes = VaultExportService.ExportEncrypted(
            [new PasswordEntry { Title = "GitHub" }],
            [],
            Passphrase,
            FastOptions);

        Assert.ThrowsAny<CryptographicException>(() =>
            VaultExportService.ImportEncrypted(bytes, Encoding.UTF8.GetBytes("not the passphrase")));
    }

    [Fact]
    public void TamperedCiphertextFails()
    {
        var bytes = VaultExportService.ExportEncrypted(
            [new PasswordEntry { Title = "GitHub" }],
            [],
            Passphrase,
            FastOptions);

        var envelope = JsonSerializer.Deserialize(bytes, VaultJsonContext.Default.EncryptedExport)!;
        var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        ciphertext[0] ^= 0xFF;
        var tampered = JsonSerializer.SerializeToUtf8Bytes(
            envelope with { Ciphertext = Convert.ToBase64String(ciphertext) },
            VaultJsonContext.Default.EncryptedExport);

        Assert.ThrowsAny<CryptographicException>(() =>
            VaultExportService.ImportEncrypted(tampered, Passphrase));
    }

    [Fact]
    public void NewerEnvelopeVersionIsRejected()
    {
        var bytes = VaultExportService.ExportEncrypted(
            [new PasswordEntry { Title = "GitHub" }],
            [],
            Passphrase,
            FastOptions);

        var envelope = JsonSerializer.Deserialize(bytes, VaultJsonContext.Default.EncryptedExport)!;
        var newer = JsonSerializer.SerializeToUtf8Bytes(
            envelope with { Version = EncryptedExport.CurrentVersion + 1 },
            VaultJsonContext.Default.EncryptedExport);

        Assert.Throws<UnsupportedVaultVersionException>(() =>
            VaultExportService.ImportEncrypted(newer, Passphrase));
    }

    [Fact]
    public void ForeignFileIsRejected()
    {
        var json = Encoding.UTF8.GetBytes("""{"Format":"something-else","Version":1}""");

        Assert.Throws<InvalidDataException>(() => VaultExportService.ImportEncrypted(json, Passphrase));
    }
}
