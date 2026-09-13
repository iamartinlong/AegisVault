using AegisVault.Platform;
using AtomUI;
using Avalonia;

namespace AegisVault.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CoreDumpGuard.DisableCoreDumps();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithAtomUIDefaultOptions()
            .WithInterFont()
            .LogToTrace();
}
