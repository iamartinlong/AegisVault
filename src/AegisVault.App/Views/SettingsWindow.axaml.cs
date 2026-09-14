using AegisVault.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AegisVault.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    private async void OnBackupClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not SettingsViewModel viewModel)
            {
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "备份密码库",
                SuggestedFileName = "vault-backup.aegis",
                DefaultExtension = "aegis",
                FileTypeChoices = [new FilePickerFileType("AegisVault 密码库") { Patterns = ["*.aegis"] }],
            });

            if (file is not null)
            {
                viewModel.SaveBackupTo(file.Path.LocalPath);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not SettingsViewModel viewModel)
            {
                return;
            }

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择要导入的 CSV 文件",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("CSV 导出") { Patterns = ["*.csv"] }],
            });

            if (files is { Count: 1 })
            {
                await Task.Run(() => viewModel.ImportCsvFrom(files[0].Path.LocalPath));
            }
        }
        catch (Exception)
        {
        }
    }
}
