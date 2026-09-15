using AegisVault.App.Localization;
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
                Title = Loc.T("Settings_BackupTitle"),
                SuggestedFileName = "vault-backup.aegis",
                DefaultExtension = "aegis",
                FileTypeChoices = [new FilePickerFileType(Loc.T("Unlock_VaultFileType")) { Patterns = ["*.aegis"] }],
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
                Title = Loc.T("Settings_ImportPickTitle"),
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType(Loc.T("Settings_CsvFileType")) { Patterns = ["*.csv"] }],
            });

            if (files is { Count: 1 })
            {
                await viewModel.ImportCsvFromAsync(files[0].Path.LocalPath);
            }
        }
        catch (Exception)
        {
        }
    }
}
