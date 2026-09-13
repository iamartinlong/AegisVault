using AegisVault.App.Services;
using AegisVault.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AegisVault.App.Views;

public partial class MainWindow : Window
{
    private ClipboardService? _clipboard;
    private AutoLockService? _autoLock;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Attach(MainViewModel viewModel, ClipboardService clipboard, AutoLockService autoLock)
    {
        DataContext = viewModel;
        _clipboard = clipboard;
        _autoLock = autoLock;

        AddHandler(PointerMovedEvent, (_, _) => _autoLock.ReportActivity(), RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, (_, _) => _autoLock.ReportActivity(), RoutingStrategies.Tunnel);

        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty && WindowState == WindowState.Minimized)
            {
                Hide();
                _autoLock.SetMinimized(true);
            }
        };

        Activated += (_, _) => _autoLock.ReportActivity();
    }

    private async void OnCopyPasswordClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_clipboard is not null && DataContext is MainViewModel viewModel)
            {
                await _clipboard.CopyAsync(viewModel.EditPassword);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnCopyTotpClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_clipboard is not null &&
                DataContext is MainViewModel viewModel &&
                viewModel.IsTotpValid)
            {
                await _clipboard.CopyAsync(viewModel.TotpCode);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnGeneratePasswordClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainViewModel viewModel)
            {
                return;
            }

            var dialog = new GeneratorWindow
            {
                DataContext = new GeneratorViewModel(_clipboard),
            };

            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrEmpty(result))
            {
                viewModel.EditPassword = result;
            }
        }
        catch (Exception)
        {
        }
    }
}
