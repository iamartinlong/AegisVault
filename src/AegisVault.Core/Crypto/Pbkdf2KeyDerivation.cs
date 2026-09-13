using System.Security.Cryptography;
using AegisVault.Core.Models;

namespace AegisVault.Core.Crypto;

/// <summary>
/// PBKDF2-HMAC-SHA256 fallback key derivation for environments where the
/// native libsodium library cannot be loaded.
/// </summary>
public sealed class Pbkdf2KeyDerivation : IKeyDerivation
{
    public const int DefaultIterations = 600_000;

    public string Algorithm => KdfParameters.AlgorithmPbkdf2Sha256;

    public SecureBuffer DeriveKey(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, KdfParameters parameters)
    {
        if (parameters.Algorithm != Algorithm)
        {
            throw new ArgumentException($"Unsupported KDF algorithm '{parameters.Algorithm}'.", nameof(parameters));
        }

        // The vault layer always generates a 16-byte salt; the derivation itself
        // accepts any non-empty salt so standard test vectors remain usable.
        if (salt.IsEmpty)
        {
            throw new ArgumentException("Salt must not be empty.", nameof(salt));
        }

        var iterations = parameters.Iterations is > 0 ? (int)parameters.Iterations : DefaultIterations;
        if (iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), "PBKDF2 iterations out of range.");
        }

        var keyLength = parameters.KeySize;
        if (keyLength is < 16 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), "Key length out of range.");
        }

        var buffer = new SecureBuffer(keyLength);
        try
        {
            Rfc2898DeriveBytes.Pbkdf2(password, salt, buffer.Span, iterations, HashAlgorithmName.SHA256);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }
}
