using System.Security.Cryptography;
using System.Text.Json;
using AegisVault.Core.Crypto;
using AegisVault.Core.Models;
using AegisVault.Core.Storage;
using Xunit;

namespace AegisVault.Core.Tests.Storage;

public sealed class EntryRepositoryTests
{
    [Fact]
    public void LegacyV1PayloadIsNormalizedOnDecrypt()
    {
        var dek = RandomNumberGenerator.GetBytes(KeyEnvelope.DekSize);
        var entry = new PasswordEntry { Title = "Legacy", Url = "https://example.com" };

        // v1 payload as written before Urls existed: the collection members are
        // missing entirely (source-generated JSON returns null for them).
        var legacyJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            entry.Id,
            entry.Title,
            entry.Username,
            entry.Password,
            entry.Url,
            entry.Notes,
            entry.TotpSecret,
            entry.Tags,
            entry.CustomFields,
            entry.IsFavorite,
            entry.CreatedAt,
            entry.UpdatedAt,
        });
        var (nonce, ciphertext, tag) = AesGcmCipher.Encrypt(
            dek,
            legacyJson,
            EntryRepository.BuildAssociatedData(entry.Id, 1));

        var decrypted = EntryRepository.Decrypt(dek, entry.Id, 1, nonce, ciphertext, tag);

        Assert.Equal("Legacy", decrypted.Title);
        Assert.NotNull(decrypted.Urls);
        Assert.Empty(decrypted.Urls);
        Assert.NotNull(decrypted.Tags);
        Assert.NotNull(decrypted.CustomFields);
    }

    [Fact]
    public void PayloadVersionIsBoundByAssociatedData()
    {
        var dek = RandomNumberGenerator.GetBytes(KeyEnvelope.DekSize);
        var entry = new PasswordEntry { Title = "Bound" };
        var (nonce, ciphertext, tag) = EntryRepository.Encrypt(dek, entry, version: 1);

        Assert.ThrowsAny<CryptographicException>(() =>
            EntryRepository.Decrypt(dek, entry.Id, 2, nonce, ciphertext, tag));
    }

    [Fact]
    public void CurrentVersionRoundTripsAllCollections()
    {
        var dek = RandomNumberGenerator.GetBytes(KeyEnvelope.DekSize);
        var entry = new PasswordEntry
        {
            Title = "Modern",
            Urls = ["https://a.example", "https://b.example"],
        };
        var (nonce, ciphertext, tag) = EntryRepository.Encrypt(dek, entry);

        var decrypted = EntryRepository.Decrypt(
            dek,
            entry.Id,
            EntryRepository.EntryFormatVersion,
            nonce,
            ciphertext,
            tag);

        Assert.Equal(["https://a.example", "https://b.example"], decrypted.Urls);
    }
}
