using AegisVault.Platform;
using AtomUI;
using Avalonia;

namespace AegisVault.App;

internal static class Program
{
    /// <summary>Window title of the running instance a second launch activates.</summary>
    internal const string WindowTitle = "AegisVault";

    [STAThread]
    public static void Main(string[] args)
    {
        CoreDumpGuard.DisableCoreDumps();

        using var instance = SingleInstanceGuard.Acquire();
        if (!instance.IsOwner)
        {
            // Another instance owns the vault: bring its window forward and exit.
            // The window may not exist yet when both launches race; the second
            // process still exits so no duplicate session ever starts.
            WindowActivation.TryActivateByTitle(WindowTitle);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithAtomUIDefaultOptions()
            .WithInterFont()
            .LogToTrace();
}
