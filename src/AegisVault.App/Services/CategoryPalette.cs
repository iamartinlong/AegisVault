using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using AtomUI.Desktop.Controls;
using AegisVault.Core.Models;

namespace AegisVault.App.Services;

/// <summary>
/// Category colours: named palette keys (theme aware) or literal hex values.
/// Stored values come from <see cref="CategoryColors"/>; anything unset falls
/// back to a stable colour derived from the category name so legacy categories
/// are not all grey.
/// </summary>
public static class CategoryPalette
{
    /// <summary>One palette slot with its light/dark rendering colours.</summary>
    public sealed record Entry(string Key, Color Light, Color Dark);

    public static readonly IReadOnlyList<Entry> Entries =
    [
        new("red", Color.Parse("#D93F3F"), Color.Parse("#E86A6A")),
        new("orange", Color.Parse("#D97A1F"), Color.Parse("#E8A05A")),
        new("gold", Color.Parse("#C2A21F"), Color.Parse("#D9BE55")),
        new("green", Color.Parse("#2E8B57"), Color.Parse("#4FB07C")),
        new("cyan", Color.Parse("#1F8A8A"), Color.Parse("#4FAFAF")),
        new("blue", Color.Parse("#2F6FD0"), Color.Parse("#6A9BE8")),
        new("purple", Color.Parse("#7A4FD0"), Color.Parse("#A98BEB")),
        new("magenta", Color.Parse("#C24F97"), Color.Parse("#E08BC0")),
    ];

    /// <summary>Palette shown inside the colour picker ("分类颜色" group), expanded by default.</summary>
    public static List<ColorPickerPalette> BuildPickerGroups()
        => [new ColorPickerPalette(
            Localization.Loc.T("Main_CategoryColorPalette"),
            true,
            [.. Entries.Select(entry => entry.Light)])];

    /// <summary>Resolves a stored colour (key/hex/empty) to a render colour.</summary>
    public static Color Resolve(string? stored, string categoryName, ThemeVariant variant)
    {
        var normalized = CategoryColors.Normalize(stored);
        if (normalized.Length > 0)
        {
            return ResolveEntry(normalized, categoryName).ColorFor(variant);
        }

        return FallbackEntry(categoryName).ColorFor(variant);
    }

    /// <summary>Converts a picked colour back to its storage form (key when it matches a preset).</summary>
    public static string ToStorage(Color? color)
    {
        if (color is not { } value)
        {
            return string.Empty;
        }

        foreach (var entry in Entries)
        {
            if (entry.Light == value)
            {
                return entry.Key;
            }
        }

        return $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    /// <summary>Converts a stored colour to a picker value (null when unset).</summary>
    public static Color? ToPickerColor(string? stored)
    {
        var normalized = CategoryColors.Normalize(stored);
        return normalized.Length == 0 ? null : ResolveEntry(normalized, string.Empty).Light;
    }

    private static Entry ResolveEntry(string normalized, string categoryName)
    {
        foreach (var entry in Entries)
        {
            if (entry.Key == normalized)
            {
                return entry;
            }
        }

        if (CategoryColors.IsHex(normalized) &&
            Color.TryParse(normalized, out var custom))
        {
            return new Entry(normalized, custom, custom);
        }

        return FallbackEntry(categoryName);
    }

    /// <summary>Stable name-derived colour (same slot for the same name).</summary>
    private static Entry FallbackEntry(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName))
        {
            return Entries[^1];
        }

        var hash = 17;
        foreach (var character in categoryName)
        {
            hash = (hash * 31) + character;
        }

        return Entries[(hash & 0x7FFFFFFF) % Entries.Count];
    }

    private static Color ColorFor(this Entry entry, ThemeVariant variant)
        => variant == ThemeVariant.Dark ? entry.Dark : entry.Light;
}
