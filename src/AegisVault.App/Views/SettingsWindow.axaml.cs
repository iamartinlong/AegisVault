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
}
