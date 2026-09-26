using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AegisVault.Core.Crypto;
using AegisVault.Core.Models;
using AegisVault.Core.Storage;

namespace AegisVault.Core.Services;

/// <summary>
/// Exports a vault as a passphrase-protected file that can be restored on another
/// machine (unlike the vault file itself, which needs this device's DEK).
/// <para>
/// The passphrase is stretched with the same KDF as a vault master password, and
/// the payload is sealed with AES-256-GCM under a domain-separated AAD, so an
/// export can never be confused with vault or settings material.
/// </para>
/// </summary>
public static class VaultExportService
{
    /// <summary>Associated data binding the ciphertext to the export domain.</summary>
    private const string AssociatedData = "AegisVault|export|v1";

    /// <summary>
    /// Serializes the live entries and categories, encrypts them under
    /// <paramref name="passphrase"/> and returns the UTF-8 JSON of the envelope.
    /// </summary>
    public static byte[] ExportEncrypted(
        IEnumerable<PasswordEntry> entries,
        IEnumerable<Category> categories,
        ReadOnlySpan<byte> passphrase,
        VaultOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(categories);

        var kdf = options?.Kdf ?? new KdfParameters();
        var payload = new ExportPayload
        {
            Version = ExportPayload.CurrentVersion,
            Entries = entries.Where(entry => entry.DeletedAt is null).ToList(),
            Categories = categories.ToList(),
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(payload, VaultJsonContext.Default.ExportPayload);
        try
        {
            var salt = RandomNumberGenerator.GetBytes(kdf.SaltSize);
            using var key = KeyDerivationFactory.Create(kdf.Algorithm).DeriveKey(passphrase, salt, kdf);
            var (nonce, ciphertext, tag) = AesGcmCipher.Encrypt(
                key.ReadOnlySpan,
                json,
                Encoding.UTF8.GetBytes(AssociatedData));

            var envelope = new EncryptedExport
            {
                Format = EncryptedExport.FormatIdentifier,
                Version = EncryptedExport.CurrentVersion,
                Kdf = kdf,
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce),
                Ciphertext = Convert.ToBase64String(ciphertext),
                Tag = Convert.ToBase64String(tag),
            };

            return JsonSerializer.SerializeToUtf8Bytes(envelope, VaultJsonContext.Default.EncryptedExport);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    /// <summary>
    /// Decrypts an export produced by <see cref="ExportEncrypted"/>. Throws
    /// <see cref="CryptographicException"/> for a wrong passphrase or a tampered
    /// file, and <see cref="UnsupportedVaultVersionException"/> for an envelope
    /// written by a newer release.
    /// </summary>
    public static ExportPayload ImportEncrypted(ReadOnlySpan<byte> json, ReadOnlySpan<byte> passphrase)
    {
        var envelope = JsonSerializer.Deserialize(json, VaultJsonContext.Default.EncryptedExport)
            ?? throw new InvalidDataException("The export file is empty.");

        if (!string.Equals(envelope.Format, EncryptedExport.FormatIdentifier, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The file is not an AegisVault export.");
        }

        if (envelope.Version > EncryptedExport.CurrentVersion)
        {
            throw new UnsupportedVaultVersionException(
                $"Export version {envelope.Version} is newer than this application supports.");
        }

        var kdf = envelope.Kdf
            ?? throw new InvalidDataException("The export file has no key derivation parameters.");

        using var key = KeyDerivationFactory.Create(kdf.Algorithm).DeriveKey(
            passphrase,
            Convert.FromBase64String(envelope.Salt),
            kdf);

        var plaintext = AesGcmCipher.Decrypt(
            key.ReadOnlySpan,
            Convert.FromBase64String(envelope.Nonce),
            Convert.FromBase64String(envelope.Ciphertext),
            Convert.FromBase64String(envelope.Tag),
            Encoding.UTF8.GetBytes(AssociatedData));

        try
        {
            var payload = JsonSerializer.Deserialize(plaintext, VaultJsonContext.Default.ExportPayload)
                ?? throw new InvalidDataException("The export payload is empty.");

            if (payload.Version > ExportPayload.CurrentVersion || payload.Version < ExportPayload.OldestSupportedVersion)
            {
                throw new UnsupportedVaultVersionException(
                    $"Export payload version {payload.Version} is not supported.");
            }

            return payload;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
