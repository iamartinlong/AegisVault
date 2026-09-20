using AegisVault.Platform;
using AtomUI;
using Avalonia;

namespace AegisVault.App;

internal static class Program
{
    /// <summary>
    /// Window titles a second launch activates. The title bar follows the UI
    /// language, so both the neutral product name and the localized one are
    /// accepted (see <c>App_Title</c>).
    /// </summary>
    internal static readonly string[] WindowTitles = ["AegisVault", Localization.Loc.Get(Localization.Loc.Chinese, "App_Title")];

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
            WindowActivation.TryActivateByAnyTitle(WindowTitles);
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
