using AegisVault.Core.Models;

namespace AegisVault.Core.Services;

/// <summary>Options used when creating a vault or changing the master password.</summary>
public sealed class VaultOptions
{
    public KdfParameters Kdf { get; init; } = new();
}
