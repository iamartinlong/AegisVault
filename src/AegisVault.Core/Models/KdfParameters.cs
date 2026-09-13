namespace AegisVault.Core.Models;

/// <summary>
/// Key derivation parameters persisted in the vault header so that
/// parameters can evolve without breaking existing vaults.
/// </summary>
public sealed record KdfParameters
{
    public const string AlgorithmArgon2id = "argon2id";
    public const string AlgorithmPbkdf2Sha256 = "pbkdf2-sha256";

    public const int DefaultSaltSize = 16;
    public const int DefaultKeySize = 32;

    public string Algorithm { get; init; } = AlgorithmArgon2id;

    /// <summary>Argon2id time cost (t) or PBKDF2 iteration count.</summary>
    public long Iterations { get; init; } = Argon2Defaults.Iterations;

    /// <summary>Argon2id memory cost in bytes (m). Ignored by PBKDF2.</summary>
    public long MemoryBytes { get; init; } = Argon2Defaults.MemoryBytes;

    public int SaltSize { get; init; } = DefaultSaltSize;

    public int KeySize { get; init; } = DefaultKeySize;

    public static class Argon2Defaults
    {
        public const long Iterations = 3;
        public const long MemoryBytes = 64L * 1024 * 1024;
    }
}
