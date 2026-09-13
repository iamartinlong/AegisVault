namespace AegisVault.Core.Models;

/// <summary>Sensitive user configuration, stored encrypted inside the vault.</summary>
public sealed record UserConfig
{
    public int AutoLockMinutes { get; init; } = 5;

    public bool LockOnMinimize { get; init; } = true;

    public bool LockOnScreenLock { get; init; } = true;

    public bool LockOnSuspend { get; init; } = true;

    /// <summary>Seconds after which copied secrets are removed from the clipboard (0 disables).</summary>
    public int ClipboardClearSeconds { get; init; } = 30;

    public PasswordGeneratorOptions Generator { get; init; } = new();
}
