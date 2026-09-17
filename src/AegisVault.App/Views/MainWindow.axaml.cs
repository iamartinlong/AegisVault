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
        Action? openSettings = null)
    {
        DataContext = viewModel;
        _clipboard = clipboard;
        _autoLock = autoLock;
        _openSettings = openSettings;
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
            _ = ConfirmDeleteEntryAsync();
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

    private void OnOpenUrlClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string url } && DataContext is MainViewModel viewModel)
        {
            viewModel.OpenUrlCommand.Execute(url);
        }
    }

    private async void OnDeleteEntryClicked(object? sender, RoutedEventArgs e)
        => await ConfirmDeleteEntryAsync();

    private async Task ConfirmDeleteEntryAsync()
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedEntry is not { } entry)
        {
            return;
        }

        try
        {
            var dialog = new ConfirmWindow(
                Loc.T("Main_DeleteEntryTitle"),
                Loc.Format("Main_DeleteEntryMessage", entry.Title));
            if (await dialog.ShowDialog<bool>(this))
            {
                viewModel.DeleteEntryCommand.Execute(null);
            }
        }
        catch (Exception)
        {
        }
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e) => _openSettings?.Invoke();

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
                name => viewModel.ValidateCategoryName(name),
                initialColor: null,
                showColorPicker: true);
            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrEmpty(result))
            {
                viewModel.TryCreateCategory(result, out _, CategoryPalette.ToStorage(dialog.SelectedColor));
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
                name => viewModel.ValidateCategoryName(name, id),
                initialColor: item.Color,
                showColorPicker: true);
            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrEmpty(result))
            {
                viewModel.TryRenameCategory(id, result, out _);
                viewModel.SetCategoryColor(id, CategoryPalette.ToStorage(dialog.SelectedColor));
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

    private void OnSortButtonClicked(object? sender, RoutedEventArgs e)
        => OpenFlyoutOnKeyboard(SortButton);

    private void OnSortFlyoutOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        // The flyout lives outside the normal binding scope, so the radio
        // state is synced explicitly whenever the menu opens.
        SortByNameItem.IsChecked = viewModel.IsSortByName;
        SortByRecentItem.IsChecked = viewModel.IsSortByRecent;
    }

    private void OnSortByNameClicked(object? sender, RoutedEventArgs e)
        => (DataContext as MainViewModel)?.SortByNameCommand.Execute(null);

    private void OnSortByRecentClicked(object? sender, RoutedEventArgs e)
        => (DataContext as MainViewModel)?.SortByRecentCommand.Execute(null);

    private void OnThemeButtonClicked(object? sender, RoutedEventArgs e)
        => OpenFlyoutOnKeyboard(ThemeButton);

    private void OnThemeFlyoutOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        // The flyout lives outside the normal binding scope, so the radio
        // state is synced explicitly whenever the menu opens.
        ThemeSystemItem.IsChecked = viewModel.ThemePreference == "system";
        ThemeLightItem.IsChecked = viewModel.ThemePreference == "light";
        ThemeDarkItem.IsChecked = viewModel.ThemePreference == "dark";
    }

    private void OnThemeSystemClicked(object? sender, RoutedEventArgs e) => SetTheme("system");

    private void OnThemeLightClicked(object? sender, RoutedEventArgs e) => SetTheme("light");

    private void OnThemeDarkClicked(object? sender, RoutedEventArgs e) => SetTheme("dark");

    private void SetTheme(string theme)
        => (DataContext as MainViewModel)?.SetThemeCommand.Execute(theme);

    private void OnLanguageButtonClicked(object? sender, RoutedEventArgs e)
        => OpenFlyoutOnKeyboard(LanguageButton);

    private void OnLanguageFlyoutOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        LanguageSystemItem.IsChecked = viewModel.LanguagePreference == Loc.System;
        LanguageZhItem.IsChecked = viewModel.LanguagePreference == Loc.Chinese;
        LanguageEnItem.IsChecked = viewModel.LanguagePreference == Loc.English;
    }

    private void OnLanguageSystemClicked(object? sender, RoutedEventArgs e) => SetLanguage(Loc.System);

    private void OnLanguageZhClicked(object? sender, RoutedEventArgs e) => SetLanguage(Loc.Chinese);

    private void OnLanguageEnClicked(object? sender, RoutedEventArgs e) => SetLanguage(Loc.English);

    private void SetLanguage(string language)
        => (DataContext as MainViewModel)?.SetLanguageCommand.Execute(language);

    /// <summary>
    /// Pointer presses open a dropdown flyout through FlyoutStateHelper; the
    /// Click event only fires for keyboard activation (Space/Enter), so the
    /// menu stays reachable without a mouse.
    /// </summary>
    private static void OpenFlyoutOnKeyboard(AtomUI.Desktop.Controls.DropdownButton button)
    {
        var flyout = button.DropdownFlyout;
        if (flyout is not null && !flyout.IsOpen)
        {
            flyout.ShowAt(button);
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
