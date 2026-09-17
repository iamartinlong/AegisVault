using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AegisVault.App.Services;

namespace AegisVault.App.Views;

/// <summary>
/// Small single-input dialog used for creating and renaming categories. The
/// optional colour row (palette + custom picker) is shown for categories only.
/// </summary>
public partial class TextPromptWindow : Window
{
    private readonly Func<string, string?>? _validator;

    /// <summary>Colour chosen in the optional picker (null when the row is hidden or unset).</summary>
    public Color? SelectedColor { get; private set; }

    public TextPromptWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public TextPromptWindow(
        string title,
        string label,
        string initialText,
        Func<string, string?>? validator = null,
        string? initialColor = null,
        bool showColorPicker = false)
        : this()
    {
        Title = title;
        TitleBar.Title = title;
        LabelText.Text = label;
        InputBox.Text = initialText;
        _validator = validator;

        if (!showColorPicker)
        {
            return;
        }

        ColorSection.IsVisible = true;
        ColorPickerBox.PaletteGroup = CategoryPalette.BuildPickerGroups();
        ColorPickerBox.Value = CategoryPalette.ToPickerColor(initialColor);
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

        SelectedColor = ColorSection.IsVisible ? ColorPickerBox.Value : null;
        Close(text);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(null);
}
