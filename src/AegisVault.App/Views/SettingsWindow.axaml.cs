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

    private async void OnExportCsvClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not SettingsViewModel viewModel)
            {
                return;
            }

            // Plaintext export: always confirm before writing secrets to disk.
            var dialog = new ConfirmWindow(
                Loc.T("Settings_ExportPlaintextTitle"),
                Loc.T("Settings_ExportPlaintextWarning"),
                Loc.T("Settings_ExportCsv"));
            if (!await dialog.ShowDialog<bool>(this))
            {
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Loc.T("Settings_ExportPickCsvTitle"),
                SuggestedFileName = "aegisvault-export.csv",
                DefaultExtension = "csv",
                FileTypeChoices = [new FilePickerFileType(Loc.T("Settings_CsvFileType")) { Patterns = ["*.csv"] }],
            });

            if (file is not null)
            {
                viewModel.ExportCsvTo(file.Path.LocalPath);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnExportEncryptedClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not SettingsViewModel viewModel)
            {
                return;
            }

            var prompt = new TextPromptWindow(
                Loc.T("Settings_ExportPickJsonTitle"),
                Loc.T("Settings_ExportPassphrase"),
                string.Empty,
                validator: text => string.IsNullOrEmpty(text) ? Loc.T("Settings_ExportPassphraseRequired") : null,
                isPassword: true,
                note: Loc.T("Settings_ExportPassphraseHint"));
            var passphrase = await prompt.ShowDialog<string?>(this);
            if (string.IsNullOrEmpty(passphrase))
            {
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Loc.T("Settings_ExportPickJsonTitle"),
                SuggestedFileName = "aegisvault-export.json",
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType(Loc.T("Settings_JsonFileType")) { Patterns = ["*.json"] }],
            });

            if (file is not null)
            {
                await viewModel.ExportEncryptedToAsync(file.Path.LocalPath, passphrase);
            }
        }
        catch (Exception)
        {
        }
    }
}
