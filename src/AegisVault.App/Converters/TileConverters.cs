using System.Globalization;
using AegisVault.App.ViewModels;
using AtomUI.Controls;
using AtomUI.Icons.AntDesign;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AegisVault.App.Converters;

/// <summary>First visible character of an entry title, used by the list tile.</summary>
public sealed class InitialConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim();
        return string.IsNullOrEmpty(text) ? "?" : text[..1].ToUpperInvariant();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Deterministic tinted tile color derived from the entry title.</summary>
public static class TilePalette
{
    private static readonly Color[] Colors =
    [
        Color.Parse("#0F6B4F"),
        Color.Parse("#0E7490"),
        Color.Parse("#7C3AED"),
        Color.Parse("#B45309"),
        Color.Parse("#BE123C"),
        Color.Parse("#475569"),
    ];

    public static Color For(string? title)
    {
        var text = title ?? string.Empty;
        var hash = 0;
        foreach (var character in text)
        {
            hash = hash * 31 + character;
        }

        return Colors[Math.Abs(hash) % Colors.Length];
    }
}

public sealed class InitialTileBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = TilePalette.For(value as string);
        return new SolidColorBrush(Color.FromArgb(31, color.R, color.G, color.B));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InitialTileForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new SolidColorBrush(TilePalette.For(value as string));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a sidebar category item to its Ant Design icon.</summary>
public sealed class CategoryIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CategoryItem item)
        {
            return null;
        }

        var name = item.Kind switch
        {
            CategoryKind.Category => "FolderOutlined",
            CategoryKind.Tag => "TagOutlined",
            _ => item.Key switch
            {
                "all" => "AppstoreOutlined",
                "favorites" => "StarOutlined",
                "weak" => "SafetyCertificateOutlined",
                "old" => "ClockCircleOutlined",
                "recent" => "HistoryOutlined",
                "recycle" => "DeleteOutlined",
                _ => "AppstoreOutlined",
            },
        };

        return AntDesignIconFactory.Create(name);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
