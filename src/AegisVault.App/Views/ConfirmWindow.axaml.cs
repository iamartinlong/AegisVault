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

    public ConfirmWindow(string title, string message, string? okText = null, string? cancelText = null)
        : this()
    {
        Title = title;
        TitleBar.Title = title;
        MessageText.Text = message;

        if (!string.IsNullOrEmpty(okText))
        {
            OkButton.Content = okText;
        }

        if (!string.IsNullOrEmpty(cancelText))
        {
            CancelButton.Content = cancelText;
        }
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}
