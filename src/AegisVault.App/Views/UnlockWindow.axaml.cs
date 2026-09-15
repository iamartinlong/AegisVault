using AegisVault.App.Localization;
using AegisVault.App.ViewModels;
using AegisVault.Platform;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AegisVault.App.Views;

public partial class UnlockWindow : Window
{
    public UnlockWindow()
    {
        InitializeComponent();
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
            var password = WindowsSecureInput.PromptForPassword("AegisVault", Loc.T("Unlock_SecureInputTitle"), parentHandle);
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
