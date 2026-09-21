using AegisVault.App.Services;
using AegisVault.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AtomUIButton = AtomUI.Desktop.Controls.Button;

namespace AegisVault.App.Views;

public partial class GeneratorWindow : Window
{
    public GeneratorWindow()
    {
        InitializeComponent();
    }

    private void OnUseClicked(object? sender, RoutedEventArgs e)
    {
        Close(DataContext is GeneratorViewModel viewModel ? viewModel.Password : null);
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
        => CopyFeedback.Flash(sender as AtomUIButton);
}
