using AegisVault.Core.Models;

namespace AegisVault.Core.Crypto;

/// <summary>Resolves a KDF implementation from a persisted algorithm identifier.</summary>
internal static class KeyDerivationFactory
{
    public static IKeyDerivation Create(string algorithm) => algorithm switch
    {
        KdfParameters.AlgorithmArgon2id => new Argon2idKeyDerivation(),
        KdfParameters.AlgorithmPbkdf2Sha256 => new Pbkdf2KeyDerivation(),
        _ => throw new NotSupportedException($"Unsupported KDF algorithm '{algorithm}'."),
    };
}
