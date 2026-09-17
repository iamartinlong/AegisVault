using Avalonia.Media;
using Avalonia.Styling;
using AegisVault.App.Localization;
using AegisVault.App.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class CategoryPaletteTests
{
    [Fact]
    public void PresetKeysRoundTripThroughStorage()
    {
        foreach (var entry in CategoryPalette.Entries)
        {
            Assert.Equal(entry.Key, CategoryPalette.ToStorage(entry.Light));

            var pickerColor = CategoryPalette.ToPickerColor(entry.Key);
            Assert.Equal(entry.Light, pickerColor);
        }
    }

    [Fact]
    public void CustomColorsAreStoredAsHex()
    {
        var custom = Color.FromRgb(0x12, 0xAB, 0x34);

        Assert.Equal("#12AB34", CategoryPalette.ToStorage(custom));
        Assert.Equal(custom, CategoryPalette.ToPickerColor("#12AB34"));
        Assert.Equal(custom, CategoryPalette.ToPickerColor("#12ab34"));
    }

    [Fact]
    public void UnsetColorMeansNoPickerValue()
    {
        Assert.Equal(string.Empty, CategoryPalette.ToStorage(null));
        Assert.Null(CategoryPalette.ToPickerColor(null));
        Assert.Null(CategoryPalette.ToPickerColor("not-a-color"));
    }

    [Fact]
    public void PresetsAreThemeAwareAndCustomColorsAreNot()
    {
        var light = CategoryPalette.Resolve("blue", "Work", ThemeVariant.Light);
        var dark = CategoryPalette.Resolve("blue", "Work", ThemeVariant.Dark);

        Assert.NotEqual(light, dark);

        var customLight = CategoryPalette.Resolve("#123456", "Work", ThemeVariant.Light);
        var customDark = CategoryPalette.Resolve("#123456", "Work", ThemeVariant.Dark);

        Assert.Equal(customLight, customDark);
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), customLight);
    }

    [Fact]
    public void UnsetColorsFallBackToAStableNameDerivedSlot()
    {
        var first = CategoryPalette.Resolve(null, "Work", ThemeVariant.Light);
        var second = CategoryPalette.Resolve(null, "Work", ThemeVariant.Light);

        Assert.Equal(first, second);
        Assert.Contains(first, CategoryPalette.Entries.Select(entry => entry.Light));
    }

    [Fact]
    public void PresetsAreAddressableByStoredKeyAndLocalisedName()
    {
        var nameKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in CategoryPalette.Entries)
        {
            Assert.Same(entry, CategoryPalette.FindPreset(entry.Key));

            var nameKey = CategoryPalette.NameKey(entry.Key);
            Assert.True(nameKeys.Add(nameKey), $"duplicate name key for {entry.Key}");
            Assert.NotEqual(nameKey, Loc.Get(Loc.Chinese, nameKey));
            Assert.NotEqual(nameKey, Loc.Get(Loc.English, nameKey));
        }

        Assert.Null(CategoryPalette.FindPreset(null));
        Assert.Null(CategoryPalette.FindPreset("not-a-key"));
        Assert.Null(CategoryPalette.FindPreset("#123456"));
    }
}
