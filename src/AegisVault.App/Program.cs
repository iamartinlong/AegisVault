using AegisVault.Platform;
using AtomUI;
using Avalonia;

namespace AegisVault.App;

internal static class Program
{
    /// <summary>Window title of the running instance a second launch activates.</summary>
    internal const string WindowTitle = "AegisVault";

    /// <summary>
    /// Held for the whole process lifetime. The restart flow releases it early
    /// so the replacement process does not mistake itself for a second instance.
    /// </summary>
    internal static SingleInstanceGuard? Instance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        CoreDumpGuard.DisableCoreDumps();

        Instance = SingleInstanceGuard.Acquire();
        if (!Instance.IsOwner)
        {
            // Another instance owns the vault: bring its window forward and exit.
            // The window may not exist yet when both launches race; the second
            // process still exits so no duplicate session ever starts.
            WindowActivation.TryActivateByTitle(WindowTitle);
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Instance.Dispose();
            Instance = null;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithAtomUIDefaultOptions()
            .WithInterFont()
            .LogToTrace();
}
