using AegisVault.Core.Models;

namespace AegisVault.Core.Services;

/// <summary>
/// Clipboard auto-clear policy: secrets are removed after a delay, and only
/// when the clipboard still contains exactly what we copied (never clobber
/// unrelated user content).
/// </summary>
public static class ClipboardPolicy
{
    public static readonly TimeSpan DefaultClearDelay = TimeSpan.FromSeconds(30);

    public static TimeSpan GetClearDelay(UserConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.ClipboardClearSeconds <= 0
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromSeconds(config.ClipboardClearSeconds);
    }

    public static bool ShouldClear(string? currentClipboard, string? copiedContent)
    {
        if (string.IsNullOrEmpty(copiedContent))
        {
            return false;
        }

        return string.Equals(currentClipboard, copiedContent, StringComparison.Ordinal);
    }
}
