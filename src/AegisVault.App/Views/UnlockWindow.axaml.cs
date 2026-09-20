using AegisVault.App.Localization;
using AegisVault.App.ViewModels;
using AegisVault.Platform;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AegisVault.App.Views;

public partial class UnlockWindow : Window
{
    public UnlockWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FocusPrimaryInput();
    }

    /// <summary>Focuses the field the user is most likely to type into.</summary>
    private void FocusPrimaryInput()
    {
        var target = DataContext is UnlockViewModel { IsCreateMode: true } ? CreatePasswordBox : OpenPasswordBox;

        // Let the first layout pass settle before moving focus (headless
        // sessions and real windows both need the input box to exist).
        Dispatcher.UIThread.Post(() => target?.Focus(), DispatcherPriority.Input);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = ResolveDroppedPath(e) is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnVaultFileDropped(object? sender, DragEventArgs e)
    {
        if (DataContext is UnlockViewModel viewModel)
        {
            viewModel.TryAcceptDroppedFile(ResolveDroppedPath(e));
        }

        e.Handled = true;
    }

    private static string? ResolveDroppedPath(DragEventArgs e)
    {
        var files = e.DataTransfer?.TryGetFiles();
        if (files is { Length: > 0 })
        {
            string? fallback = null;
            foreach (var file in files)
            {
                var local = file.Path.LocalPath;
                if (string.IsNullOrWhiteSpace(local))
                {
                    continue;
                }

                if (local.EndsWith(".aegis", StringComparison.OrdinalIgnoreCase))
                {
                    return local;
                }

                fallback ??= local;
            }

            if (fallback is not null)
            {
                return fallback;
            }
        }

        var text = e.DataTransfer?.TryGetText();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private void OnSecureInputClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || DataContext is not UnlockViewModel viewModel)
            {
                return;
            }

            var parentHandle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            var password = WindowsSecureInput.PromptForPassword(Loc.T("App_Title"), Loc.T("Unlock_SecureInputTitle"), parentHandle);
            if (!string.IsNullOrEmpty(password))
            {
                viewModel.MasterPassword = password;
            }
        }
        catch (Exception)
        {
            if (DataContext is UnlockViewModel viewModel)
            {
                viewModel.ErrorMessage = Loc.T("Unlock_SecureInputUnavailable");
            }
        }
    }

    private async void OnBrowseExistingClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.T("Unlock_PickVaultTitle"),
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType(Loc.T("Unlock_VaultFileType")) { Patterns = ["*.aegis"] }],
            });

            if (files.Count > 0 && DataContext is UnlockViewModel viewModel)
            {
                viewModel.VaultPath = files[0].Path.LocalPath;
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnBrowseCreateClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not UnlockViewModel viewModel)
            {
                return;
            }

            IStorageFolder? startLocation = null;
            var directory = Path.GetDirectoryName(viewModel.NewVaultPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                startLocation = await StorageProvider.TryGetFolderFromPathAsync(directory);
            }

            var fileName = Path.GetFileName(viewModel.NewVaultPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "vault.aegis";
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Loc.T("Unlock_PickSaveTitle"),
                SuggestedFileName = fileName,
                DefaultExtension = "aegis",
                FileTypeChoices = [new FilePickerFileType(Loc.T("Unlock_VaultFileType")) { Patterns = ["*.aegis"] }],
                SuggestedStartLocation = startLocation,
            });

            if (file is not null)
            {
                viewModel.NewVaultPath = file.Path.LocalPath;
            }
        }
        catch (Exception)
        {
        }
    }
}
