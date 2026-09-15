using AegisVault.App.Localization;
using AegisVault.App.Services;
using AegisVault.App.ViewModels;
using AegisVault.Core.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AegisVault.App.Views;

public partial class MainWindow : Window
{
    private ClipboardService? _clipboard;
    private AutoLockService? _autoLock;
    private Action? _openSettings;
    private Action? _toggleTheme;
    private bool _handlersAttached;

    /// <summary>Raised when the user asks to unlock from the lock overlay.</summary>
    public event Action? UnlockRequested;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void ShowLockOverlay()
    {
        ClipboardToast.IsVisible = false;
        LockOverlay.IsVisible = true;
        UnlockButton.Focus();
    }

    public void HideLockOverlay() => LockOverlay.IsVisible = false;

    private void OnUnlockClicked(object? sender, RoutedEventArgs e) => UnlockRequested?.Invoke();

    public void Attach(
        MainViewModel viewModel,
        ClipboardService clipboard,
        AutoLockService autoLock,
        Action? openSettings = null,
        Action? toggleTheme = null)
    {
        DataContext = viewModel;
        _clipboard = clipboard;
        _autoLock = autoLock;
        _openSettings = openSettings;
        _toggleTheme = toggleTheme;
        HideLockOverlay();

        if (_handlersAttached)
        {
            // Unlock after a lock re-attaches a fresh session; handlers must
            // not accumulate across lock/unlock cycles.
            return;
        }

        _handlersAttached = true;

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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (LockOverlay.IsVisible)
        {
            // While locked in place the window must not react to any shortcut.
            if (e.Key is Key.Enter or Key.Space && e.KeyModifiers == KeyModifiers.None)
            {
                UnlockRequested?.Invoke();
                e.Handled = true;
                return;
            }

            e.Handled = true;
            base.OnKeyDown(e);
            return;
        }

        if (DataContext is not MainViewModel viewModel)
        {
            base.OnKeyDown(e);
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var focusedIsTextBox = FocusManager?.GetFocusedElement() is TextBox;

        if (ctrl && !shift && !alt && e.Key == Key.F)
        {
            SearchBox.Focus();
            e.Handled = true;
        }
        else if (ctrl && !shift && !alt && e.Key == Key.N)
        {
            viewModel.AddEntryCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && !shift && !alt && e.Key == Key.E)
        {
            viewModel.BeginEditCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && !shift && !alt && e.Key == Key.S)
        {
            viewModel.SaveEntryCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && !shift && !alt && e.Key == Key.G)
        {
            OnGeneratePasswordClicked(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (ctrl && shift && !alt && e.Key == Key.C)
        {
            viewModel.CopyPasswordCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && alt && !shift && e.Key == Key.C)
        {
            viewModel.CopyTotpCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && shift && !alt && e.Key == Key.L)
        {
            viewModel.LockCommand.Execute(null);
            e.Handled = true;
        }
        else if (!ctrl && e.Key == Key.Delete && !focusedIsTextBox)
        {
            viewModel.DeleteEntryCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && viewModel.IsEditing)
        {
            viewModel.CancelEditCommand.Execute(null);
            e.Handled = true;
        }
        else
        {
            base.OnKeyDown(e);
        }
    }

    private void OnRowCopyUsernameClicked(object? sender, RoutedEventArgs e)
        => InvokeForRow(sender, viewModel => viewModel.CopyUsernameCommand.Execute(null));

    private void OnSettingsClicked(object? sender, RoutedEventArgs e) => _openSettings?.Invoke();

    private void OnThemeToggleClicked(object? sender, RoutedEventArgs e) => _toggleTheme?.Invoke();

    private void OnRowCopyPasswordClicked(object? sender, RoutedEventArgs e)
        => InvokeForRow(sender, viewModel => viewModel.CopyPasswordCommand.Execute(null));

    private void OnRowCopyTotpClicked(object? sender, RoutedEventArgs e)
        => InvokeForRow(sender, viewModel => viewModel.CopyTotpCommand.Execute(null));

    private async void OnNewCategoryClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            var dialog = new TextPromptWindow(
                Loc.T("Main_NewCategoryTitle"),
                Loc.T("Main_CategoryNameLabel"),
                string.Empty,
                name => viewModel.ValidateCategoryName(name));
            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrEmpty(result))
            {
                viewModel.TryCreateCategory(result, out _);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnRenameCategoryClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CategoryItem item } ||
            item.CategoryId is not { } id ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            var dialog = new TextPromptWindow(
                Loc.T("Main_RenameCategoryTitle"),
                Loc.T("Main_CategoryNameLabel"),
                item.DisplayName,
                name => viewModel.ValidateCategoryName(name, id));
            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrEmpty(result))
            {
                viewModel.TryRenameCategory(id, result, out _);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnDeleteCategoryClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CategoryItem item } ||
            item.CategoryId is not { } id ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            var dialog = new ConfirmWindow(
                Loc.T("Main_DeleteCategoryTitle"),
                Loc.Format("Main_DeleteCategoryMessage", item.DisplayName));
            if (await dialog.ShowDialog<bool>(this))
            {
                viewModel.DeleteCategory(id);
            }
        }
        catch (Exception)
        {
        }
    }

    private void InvokeForRow(object? sender, Action<MainViewModel> action)
    {
        if (sender is Button { DataContext: PasswordEntry entry } && DataContext is MainViewModel viewModel)
        {
            viewModel.SelectedEntry = entry;
            action(viewModel);
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
