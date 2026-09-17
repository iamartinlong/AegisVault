namespace AegisVault.Core.Models;

/// <summary>
/// Non-sensitive application preferences kept in a plaintext local file.
/// Secrets (vault contents, device keys) never belong here.
/// </summary>
public sealed record AppPreferences
{
    /// <summary>Last successfully opened vault path (filled at startup).</summary>
    public string? LastVaultPath { get; init; }

    /// <summary>
    /// Recently opened/created vault paths, newest first (max three). Plaintext
    /// like the rest of this file; missing values are normalised to an empty
    /// list by the store.
    /// </summary>
    public IReadOnlyList<string>? RecentVaultPaths { get; init; }

    /// <summary>True once the one-time first-run guide has been dismissed.</summary>
    public bool StartupGuideDismissed { get; init; }

    /// <summary>"system" | "light" | "dark".</summary>
    public string Theme { get; init; } = "system";

    /// <summary>"system" | "zh" | "en". Applied at startup; changing it requires a restart.</summary>
    public string Language { get; init; } = "system";

    /// <summary>Shows the optional floating quick-access ball (default off).</summary>
    public bool ShowFloatingBall { get; init; }

    /// <summary>Floating ball position in screen pixels ("x,y"); null when never moved.</summary>
    public string? BallPosition { get; init; }

    /// <summary>"left" | "right" while the ball is docked to a screen edge; otherwise null.</summary>
    public string? BallDockedSide { get; init; }
}
