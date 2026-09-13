using AegisVault.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

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
}
