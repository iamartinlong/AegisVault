using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using AegisVault.App.Services;
using AegisVault.App.ViewModels;

namespace AegisVault.App.Converters;

/// <summary>
/// Renders a category's stored colour as a brush for the sidebar dot. Known
/// palette keys stay theme aware; unset colours fall back to a stable
/// name-derived slot. Brushes are cached (they are immutable in practice).
/// </summary>
public sealed class CategoryDotConverter : IValueConverter
{
    private static readonly Dictionary<(uint Argb, bool Dark), IBrush> Cache = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CategoryItem item)
        {
            return null;
        }

        var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

        // The "uncategorized" pseudo category stays neutral.
        if (item.Kind == CategoryKind.Category && item.CategoryId is null)
        {
            return ThemedBrush("ColorTextTertiary") ?? CacheBrush(Color.FromRgb(0x9A, 0x9A, 0xA2), dark);
        }

        var color = CategoryPalette.Resolve(item.Color, item.DisplayName, dark ? ThemeVariant.Dark : ThemeVariant.Light);
        return CacheBrush(color, dark);
    }

    private static IBrush CacheBrush(Color color, bool dark)
    {
        var key = (Argb: color.ToUInt32(), Dark: dark);
        if (Cache.TryGetValue(key, out var brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(color);
        Cache[key] = brush;
        return brush;
    }

    private static IBrush? ThemedBrush(string resourceKey)
    {
        if (Application.Current is not { } app)
        {
            return null;
        }

        return app.TryFindResource(resourceKey, app.ActualThemeVariant, out var resource) && resource is IBrush brush
            ? brush
            : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
