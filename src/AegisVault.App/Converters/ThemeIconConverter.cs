using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AegisVault.App.Converters;

/// <summary>
/// Maps the stored theme preference to a mode icon: light shows a sun, dark a
/// moon and "follow the system" a desktop, so the toolbar button always tells
/// which mode is active.
/// </summary>
public sealed class ThemeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AntDesignIconFactory.Create((value as string) switch
        {
            "light" => "SunOutlined",
            "dark" => "MoonOutlined",
            _ => "DesktopOutlined",
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
