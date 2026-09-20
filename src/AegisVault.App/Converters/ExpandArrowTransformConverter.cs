using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace AegisVault.App.Converters;

/// <summary>
/// Points a category row's expander arrow down while its branch is open. The
/// app owns the NavMenu row template, so the arrow state is driven by the
/// header's <c>IsSubMenuOpen</c> instead of the stock theme's transform.
/// </summary>
public sealed class ExpandArrowTransformConverter : IValueConverter
{
    private static readonly ITransform Closed = TransformOperations.Parse("rotate(0deg)");
    private static readonly ITransform Open = TransformOperations.Parse("rotate(90deg)");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Open : Closed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
