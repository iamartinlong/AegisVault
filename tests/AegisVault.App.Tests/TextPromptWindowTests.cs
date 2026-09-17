using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using AegisVault.App.Services;
using AegisVault.App.Views;
using Xunit;

namespace AegisVault.App.Tests;

/// <summary>
/// The colour row used to rely on the AtomUI dropdown picker, whose popup
/// renders inside the window overlay and was clipped by the small dialog.
/// Presets are tiled instead and the picker is embedded.
/// </summary>
public sealed class TextPromptWindowTests
{
    private const int CustomSwatchIndex = 8;

    [Fact]
    public Task ClickingAPresetSwatchSelectsItsKey() => Headless.Run(() =>
    {
        var window = Open();
        try
        {
            Click(window, ColorSwatchRow(window).Children[2]);

            Assert.Equal("gold", CategoryPalette.ToStorage(window.SelectedColor));
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task UntouchedColourRowLeavesTheColourUnset() => Headless.Run(() =>
    {
        var window = Open();
        try
        {
            Assert.Null(window.SelectedColor);
            Assert.False(CustomPickerView(window).IsVisible);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task CustomColoursOpenTheInlinePicker() => Headless.Run(() =>
    {
        var window = Open(initialColor: "#123456");
        try
        {
            Assert.True(CustomPickerView(window).IsVisible);
            Assert.Equal("#123456", CategoryPalette.ToStorage(window.SelectedColor));
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task CustomSwatchTogglesTheInlinePicker() => Headless.Run(() =>
    {
        var window = Open();
        try
        {
            var collapsed = window.Bounds.Height;

            Click(window, ColorSwatchRow(window).Children[CustomSwatchIndex]);
            Assert.True(CustomPickerView(window).IsVisible);
            Assert.True(window.Bounds.Height > collapsed, "dialog did not grow");

            Click(window, ColorSwatchRow(window).Children[CustomSwatchIndex]);
            Assert.False(CustomPickerView(window).IsVisible);
            Assert.Equal(collapsed, window.Bounds.Height, 1);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task ChoosingAPresetCollapsesThePicker() => Headless.Run(() =>
    {
        var window = Open();
        try
        {
            var collapsed = window.Bounds.Height;

            Click(window, ColorSwatchRow(window).Children[CustomSwatchIndex]);
            Assert.True(CustomPickerView(window).IsVisible);

            Click(window, ColorSwatchRow(window).Children[2]);

            // A blank gap would remain if the dialog kept the expanded height.
            Assert.False(CustomPickerView(window).IsVisible);
            Assert.Equal(collapsed, window.Bounds.Height, 1);
            Assert.Equal("gold", CategoryPalette.ToStorage(window.SelectedColor));
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task MixingACustomColourStoresHex() => Headless.Run(() =>
    {
        var window = Open(initialColor: "blue");
        try
        {
            Click(window, ColorSwatchRow(window).Children[CustomSwatchIndex]);
            CustomPickerView(window).Value = Color.FromRgb(0x12, 0xAB, 0x34);

            Assert.Equal("#12AB34", CategoryPalette.ToStorage(window.SelectedColor));
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task PresetClickedAfterACustomColourStoresTheKeyAgain() => Headless.Run(() =>
    {
        var window = Open(initialColor: "#123456");
        try
        {
            Click(window, ColorSwatchRow(window).Children[5]);

            Assert.Equal("blue", CategoryPalette.ToStorage(window.SelectedColor));
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task InlinePickerLaysOutInsideTheDialog() => Headless.Run(() =>
    {
        var window = Open(initialColor: "#123456");
        try
        {
            var picker = CustomPickerView(window);
            Assert.True(
                picker.Bounds is { Width: > 0, Height: > 0 },
                $"inline picker has no size: {picker.Bounds}");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task ExpandingThePickerGrowsTheDialog() => Headless.Run(() =>
    {
        var window = Open();
        try
        {
            var picker = CustomPickerView(window);
            var before = window.Bounds.Height;

            Click(window, ColorSwatchRow(window).Children[CustomSwatchIndex]);

            Assert.True(
                picker.Bounds is { Width: > 0, Height: > 0 },
                $"inline picker has no size after expanding: {picker.Bounds}");
            Assert.True(
                window.Bounds.Height > before,
                $"dialog did not grow: {before} -> {window.Bounds.Height}");
        }
        finally
        {
            window.Close();
        }
    });

    private static TextPromptWindow Open(string? initialColor = null)
    {
        var window = new TextPromptWindow(
            "title",
            "name",
            "Work",
            validator: null,
            initialColor: initialColor,
            showColorPicker: true);
        window.Show();

        // Let SizeToContent settle so height comparisons start from the final
        // collapsed size rather than the headless default window size.
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static StackPanel ColorSwatchRow(TextPromptWindow window)
        => window.FindControl<StackPanel>("ColorSwatchRow")!;

    private static AtomUI.Desktop.Controls.ColorPickerView CustomPickerView(TextPromptWindow window)
        => window.FindControl<AtomUI.Desktop.Controls.ColorPickerView>("CustomPicker")!;

    private static void Click(TextPromptWindow window, Control control)
    {
        var centre = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var point = control.TranslatePoint(centre, window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}
