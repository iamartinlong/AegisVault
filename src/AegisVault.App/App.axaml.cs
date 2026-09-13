using AegisVault.App.ViewModels;
using AegisVault.App.Views;
using AegisVault.Core.Services;
using AtomUI;
using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AegisVault.App;

public partial class App : Application
{
    private bool _initialWindowAssigned;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        this.UseAtomUI(builder =>
        {
            builder.UseDesktopControls();
        });

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ShowUnlock(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowUnlock(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var viewModel = new UnlockViewModel();
        var window = new UnlockWindow { DataContext = viewModel };

        viewModel.VaultOpened += vault => ShowMain(desktop, window, vault);

        desktop.MainWindow = window;
        if (_initialWindowAssigned)
        {
            window.Show();
        }

        _initialWindowAssigned = true;
    }

    private void ShowMain(IClassicDesktopStyleApplicationLifetime desktop, UnlockWindow unlockWindow, VaultService vault)
    {
        var viewModel = new MainViewModel(vault);
        var window = new MainWindow { DataContext = viewModel };

        viewModel.LockRequested += () =>
        {
            viewModel.Dispose();
            vault.Dispose();
            ShowUnlock(desktop);
            window.Close();
        };

        window.Closed += (_, _) =>
        {
            viewModel.Dispose();
            vault.Dispose();
        };

        desktop.MainWindow = window;
        window.Show();
        unlockWindow.Close();
    }
}
