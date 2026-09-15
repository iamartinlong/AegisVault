using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AegisVault.App.Views;

/// <summary>Small confirmation dialog (returns true when confirmed).</summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
    {
        InitializeComponent();
    }

    public ConfirmWindow(string title, string message)
        : this()
    {
        Title = title;
        TitleBar.Title = title;
        MessageText.Text = message;
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}
