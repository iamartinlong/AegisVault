using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AegisVault.App.Views;

/// <summary>Small single-input dialog used for creating and renaming categories.</summary>
public partial class TextPromptWindow : Window
{
    private readonly Func<string, string?>? _validator;

    public TextPromptWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public TextPromptWindow(string title, string label, string initialText, Func<string, string?>? validator = null)
        : this()
    {
        Title = title;
        TitleBar.Title = title;
        LabelText.Text = label;
        InputBox.Text = initialText;
        _validator = validator;
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e)
    {
        var text = InputBox.Text ?? string.Empty;
        var error = _validator?.Invoke(text);
        if (error is not null)
        {
            ErrorBlock.Text = error;
            ErrorBlock.IsVisible = true;
            return;
        }

        Close(text);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(null);
}
