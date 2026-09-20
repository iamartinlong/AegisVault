using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace AegisVault.App.Converters;

/// <summary>
/// Indents a category row by its depth. The inline NavMenu header exposes the
/// nesting level, and the app owns the row template, so the indent step lives
/// here instead of depending on the control token.
/// <para>
/// AtomUI's <c>Level</c> is zero based (top level = 0, see
/// <c>NavMenuEntryContainerCoordinator</c>), so the top level stays flush with the
/// other sidebar lists and every level below adds one step.
/// </para>
/// </summary>
public sealed class CategoryIndentConverter : IValueConverter
{
    public const double IndentPerLevel = 16;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is int depth ? depth : 0;
        return new Thickness(Math.Max(0, level) * IndentPerLevel, 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
