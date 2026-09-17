using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using AegisVault.App.Localization;
using AegisVault.App.Services;
using AegisVault.App.Theme;
using AegisVault.Core.Models;
using AtomUIColorChangedEventArgs = AtomUI.Desktop.Controls.ColorChangedEventArgs;

namespace AegisVault.App.Views;

/// <summary>
/// Small single-input dialog used for creating and renaming categories. The
/// optional colour row (tiled presets plus an inline picker) is shown for
/// categories only. The picker is embedded instead of the AtomUI dropdown
/// because its popup renders inside the window overlay and would be clipped.
/// </summary>
public partial class TextPromptWindow : Window
{
    private readonly Func<string, string?>? _validator;
    private readonly string _categoryName = string.Empty;
    private readonly Dictionary<string, Border> _swatches = new(StringComparer.Ordinal);
    private Border? _customSwatch;
    private TextBlock? _customGlyph;
    private string? _selectedKey;
    private bool _colorTouched;
    private bool _syncingPicker;

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

        _categoryName = initialText;
        ColorSection.IsVisible = true;
        BuildSwatches();

        var normalized = CategoryColors.Normalize(initialColor);
        _selectedKey = CategoryPalette.FindPreset(normalized)?.Key;
        _colorTouched = normalized.Length > 0;

        _syncingPicker = true;
        CustomPicker.Value = CategoryPalette.Resolve(normalized, _categoryName, ThemeVariant.Light);
        _syncingPicker = false;

        // Custom (hex) colours open the picker right away so tweaking is one click.
        SetPickerExpanded(CategoryColors.IsHex(normalized));
        CustomPicker.ValueChanged += OnPickerValueChanged;
        RefreshSelection();
    }

    private void BuildSwatches()
    {
        var variant = ActualThemeVariant;
        foreach (var entry in CategoryPalette.Entries)
        {
            var chip = BuildChip(
                CategoryPalette.Resolve(entry.Key, _categoryName, variant),
                $"ColorSwatch-{entry.Key}",
                Loc.T(CategoryPalette.NameKey(entry.Key)),
                entry.Key,
                variant);
            _swatches[entry.Key] = chip;
            ColorSwatchRow.Children.Add(chip);
        }

        _customGlyph = new TextBlock
        {
            Text = "+",
            FontSize = 16,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = new SolidColorBrush(AppTheme.TokenColor("ColorTextSecondary", variant)),
        };
        _customSwatch = BuildChip(null, "CategoryCustomColor", Loc.T("Main_CategoryColorCustom"), "custom", variant);
        ColorSwatchRow.Children.Add(_customSwatch);
    }

    private Border BuildChip(Color? color, string automationId, string name, string tag, ThemeVariant variant)
    {
        var inner = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(5),
            Background = color is { } value ? new SolidColorBrush(value) : Brushes.Transparent,
            BorderBrush = new SolidColorBrush(AppTheme.TokenColor("ColorBorderSecondary", variant)),
            BorderThickness = color is null ? new Thickness(1) : default,
        };

        if (color is null && _customGlyph is not null)
        {
            inner.Child = _customGlyph;
        }

        var chip = new Border
        {
            Padding = new Thickness(3),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = inner,
            Tag = tag,
        };
        chip.PointerPressed += OnSwatchPressed;

        AutomationProperties.SetAutomationId(chip, automationId);
        AutomationProperties.SetName(chip, name);
        ToolTip.SetTip(chip, name);
        return chip;
    }

    private void OnSwatchPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: string tag })
        {
            return;
        }

        if (tag == "custom")
        {
            SetPickerExpanded(!CustomPicker.IsVisible);
        }
        else if (CategoryPalette.FindPreset(tag) is { } preset)
        {
            _selectedKey = preset.Key;
            _colorTouched = true;
            _syncingPicker = true;
            CustomPicker.Value = preset.Light;
            _syncingPicker = false;
            SetPickerExpanded(false);
        }

        RefreshSelection();
        e.Handled = true;
    }

    /// <summary>
    /// Shows or hides the inline picker. Both transitions need an explicit
    /// measure invalidation: a collapsed control keeps its zero measure cache
    /// when it becomes visible (the dialog would never grow), and Avalonia skips
    /// measure invalidation for controls that just became invisible, so the
    /// parent keeps the expanded height (the dialog would keep a blank gap).
    /// </summary>
    private void SetPickerExpanded(bool expanded)
    {
        if (CustomPicker.IsVisible == expanded)
        {
            return;
        }

        CustomPicker.IsVisible = expanded;
        CustomPicker.InvalidateMeasure();
        (CustomPicker.Parent as Layoutable)?.InvalidateMeasure();
    }

    private void OnPickerValueChanged(object? sender, AtomUIColorChangedEventArgs e)
    {
        if (_syncingPicker)
        {
            return;
        }

        _selectedKey = null;
        _colorTouched = true;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        var accent = new SolidColorBrush(AppTheme.TokenColor("ColorPrimary", ActualThemeVariant));
        foreach (var (key, chip) in _swatches)
        {
            chip.BorderBrush = key == _selectedKey ? accent : Brushes.Transparent;
        }

        // Untouched rows stay unset so editing never freezes the name-derived fallback.
        SelectedColor = _colorTouched ? CustomPicker.Value : null;

        if (_customSwatch is null)
        {
            return;
        }

        var customActive = _selectedKey is null && _colorTouched;
        _customSwatch.BorderBrush = customActive ? accent : Brushes.Transparent;

        if (_customSwatch.Child is Border inner)
        {
            inner.Background = customActive ? new SolidColorBrush(CustomPicker.Value) : Brushes.Transparent;
            inner.BorderThickness = customActive ? default : new Thickness(1);
        }

        if (_customGlyph is not null)
        {
            _customGlyph.IsVisible = !customActive;
        }
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
