using System.Security.Cryptography;

namespace AegisVault.Core.Crypto;

/// <summary>
/// Layered key envelope: wraps the data encryption key (DEK) with the
/// password-derived key encryption key (KEK) using AES-256-GCM.
/// Blob layout: nonce (12) || ciphertext (32) || tag (16).
/// </summary>
public static class KeyEnvelope
{
    public const int DekSize = 32;
    public const int BlobSize = AesGcmCipher.NonceSize + DekSize + AesGcmCipher.TagSize;

    public static byte[] Wrap(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> dek, ReadOnlySpan<byte> associatedData)
    {
        if (dek.Length != DekSize)
        {
            throw new ArgumentException($"DEK must be {DekSize} bytes.", nameof(dek));
        }

        var (nonce, ciphertext, tag) = AesGcmCipher.Encrypt(kek, dek, associatedData);

        var blob = new byte[BlobSize];
        nonce.CopyTo(blob.AsSpan(0, AesGcmCipher.NonceSize));
        ciphertext.CopyTo(blob.AsSpan(AesGcmCipher.NonceSize, ciphertext.Length));
        tag.CopyTo(blob.AsSpan(AesGcmCipher.NonceSize + ciphertext.Length, tag.Length));
        return blob;
    }

    /// <summary>
    /// Unwraps the DEK into a <see cref="SecureBuffer"/>. Throws
    /// <see cref="CryptographicException"/> when the KEK or AAD is wrong.
    /// </summary>
    public static SecureBuffer Unwrap(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> blob, ReadOnlySpan<byte> associatedData)
    {
        if (blob.Length != BlobSize)
        {
            throw new ArgumentException($"Envelope must be {BlobSize} bytes.", nameof(blob));
        }

        var nonce = blob[..AesGcmCipher.NonceSize];
        var ciphertext = blob.Slice(AesGcmCipher.NonceSize, DekSize);
        var tag = blob[^AesGcmCipher.TagSize..];

        var dek = AesGcmCipher.Decrypt(kek, nonce, ciphertext, tag, associatedData);
        try
        {
            return SecureBuffer.From(dek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }
}
