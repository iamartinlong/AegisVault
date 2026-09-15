using System.Diagnostics;

namespace AegisVault.App.Services;

/// <summary>Opens URLs in the system default browser.</summary>
public interface IUrlLauncher
{
    /// <summary>True when the URL is a usable http/https address.</summary>
    bool IsSupported(string? url);

    /// <summary>Opens the URL; returns false without side effects when unsupported or failing.</summary>
    bool TryOpen(string? url);
}

public sealed class SystemUrlLauncher : IUrlLauncher
{
    public static SystemUrlLauncher Instance { get; } = new();

    public bool IsSupported(string? url)
    {
        var value = url?.Trim();
        return !string.IsNullOrEmpty(value) &&
               (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    public bool TryOpen(string? url)
    {
        if (!IsSupported(url))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url!.Trim()) { UseShellExecute = true });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
