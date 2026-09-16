using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AegisVault.Core.Crypto;
using AegisVault.Core.Models;

namespace AegisVault.Core.Storage;

/// <summary>
/// Converts between <see cref="PasswordEntry"/> models and their encrypted
/// on-disk representation (AES-256-GCM with entry-bound associated data).
/// </summary>
internal static class EntryRepository
{
    public const int EntryFormatVersion = 1;

    public static (byte[] Nonce, byte[] Ciphertext, byte[] Tag) Encrypt(ReadOnlySpan<byte> dek, PasswordEntry entry)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(entry, VaultJsonContext.Default.PasswordEntry);
        try
        {
            return AesGcmCipher.Encrypt(dek, json, BuildAssociatedData(entry.Id, EntryFormatVersion));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    public static PasswordEntry Decrypt(
        ReadOnlySpan<byte> dek,
        Guid id,
        int version,
        byte[] nonce,
        byte[] ciphertext,
        byte[] tag)
    {
        var json = AesGcmCipher.Decrypt(dek, nonce, ciphertext, tag, BuildAssociatedData(id, version));
        try
        {
            var entry = JsonSerializer.Deserialize(json, VaultJsonContext.Default.PasswordEntry)
                ?? throw new InvalidDataException("Entry payload is empty.");

            // Source-generated JSON yields null for collection properties that
            // are missing from older vault files (property initializers are not
            // applied); normalize them so consumers can rely on non-null lists.
            if (entry.Tags is null || entry.Urls is null || entry.CustomFields is null)
            {
                entry = entry with
                {
                    Tags = entry.Tags is null ? [] : entry.Tags,
                    Urls = entry.Urls is null ? [] : entry.Urls,
                    CustomFields = entry.CustomFields is null ? [] : entry.CustomFields,
                };
            }

            return entry;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    /// <summary>Associated data binding a ciphertext to its entry id and format version.</summary>
    public static byte[] BuildAssociatedData(Guid id, int version)
        => Encoding.UTF8.GetBytes($"AegisVault|entry|{id:D}|v{version}");
}
