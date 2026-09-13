using AegisVault.Core.Models;

namespace AegisVault.Core.Crypto;

/// <summary>Derives a key encryption key (KEK) from the master password.</summary>
public interface IKeyDerivation
{
    /// <summary>Algorithm identifier persisted in the vault header.</summary>
    string Algorithm { get; }

    SecureBuffer DeriveKey(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, KdfParameters parameters);
}
