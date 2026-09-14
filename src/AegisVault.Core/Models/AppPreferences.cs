namespace AegisVault.Core.Models;

/// <summary>
/// Non-sensitive application preferences kept in a plaintext local file.
/// Secrets (vault contents, device keys) never belong here.
/// </summary>
public sealed record AppPreferences
{
    /// <summary>Last successfully opened vault path (filled at startup).</summary>
    public string? LastVaultPath { get; init; }

    /// <summary>"system" | "light" | "dark".</summary>
    public string Theme { get; init; } = "system";
}
