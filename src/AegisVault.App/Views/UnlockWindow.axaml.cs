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
            var password = WindowsSecureInput.PromptForPassword("AegisVault", "请输入主密码（系统安全桌面输入）", parentHandle);
            if (!string.IsNullOrEmpty(password))
            {
                viewModel.MasterPassword = password;
            }
        }
        catch (Exception)
        {
            if (DataContext is UnlockViewModel viewModel)
            {
                viewModel.ErrorMessage = "安全输入不可用，请手动输入主密码。";
            }
        }
    }

    private async void OnBrowseExistingClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择密码库文件",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("AegisVault 密码库") { Patterns = ["*.aegis"] }],
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
                Title = "选择密码库保存位置",
                SuggestedFileName = fileName,
                DefaultExtension = "aegis",
                FileTypeChoices = [new FilePickerFileType("AegisVault 密码库") { Patterns = ["*.aegis"] }],
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
