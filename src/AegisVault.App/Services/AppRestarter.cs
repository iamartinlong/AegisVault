using System;
using System.Diagnostics;

namespace AegisVault.App.Services;

/// <summary>Starts a fresh instance of the application (used to apply a new language).</summary>
public interface IAppRestarter
{
    /// <summary>
    /// Starts a detached copy of the current executable; returns false when the
    /// executable path is unknown so callers can fall back to a manual restart.
    /// </summary>
    bool TryStartNewInstance();
}

public sealed class ProcessAppRestarter : IAppRestarter
{
    public static ProcessAppRestarter Instance { get; } = new();

    public bool TryStartNewInstance()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception)
        {
            // A failed spawn must not prevent the current instance from closing.
            return false;
        }
    }
}
