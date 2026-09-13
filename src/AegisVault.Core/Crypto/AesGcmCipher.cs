using System.Security.Cryptography;

namespace AegisVault.Core.Crypto;

/// <summary>
/// AES-256-GCM authenticated encryption helpers.
/// Every encryption uses a fresh random 96-bit nonce.
/// </summary>
public static class AesGcmCipher
{
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;

    public static (byte[] Nonce, byte[] Ciphertext, byte[] Tag) Encrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData)
    {
        ValidateKey(key);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var gcm = new AesGcm(key, TagSize);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return (nonce, ciphertext, tag);
    }

    /// <summary>
    /// Decrypts and authenticates. Throws <see cref="CryptographicException"/> when
    /// the key, nonce, tag or associated data do not match.
    /// </summary>
    public static byte[] Decrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag,
        ReadOnlySpan<byte> associatedData)
    {
        ValidateKey(key);
        if (nonce.Length != NonceSize)
        {
            throw new ArgumentException($"Nonce must be {NonceSize} bytes.", nameof(nonce));
        }

        if (tag.Length != TagSize)
        {
            throw new ArgumentException($"Tag must be {TagSize} bytes.", nameof(tag));
        }

        var plaintext = new byte[ciphertext.Length];
        using var gcm = new AesGcm(key, TagSize);
        gcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        return plaintext;
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));
        }
    }
}
