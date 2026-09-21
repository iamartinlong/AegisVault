using AtomUI.Icons.AntDesign;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace AegisVault.App.Tests;

/// <summary>
/// AtomUI icons derive from <see cref="PathIcon"/>, but style type selectors do
/// not match derived types and the icon theme wins over host values. Icon
/// colors must therefore come from an <c>atom:Button</c>'s <c>Foreground</c>
/// (the button template forwards it to the icon) instead of styles or
/// inherited values.
/// </summary>
public sealed class IconStylingTests
{
    [Fact]
    public Task BaseTypeStyleDoesNotColorAntDesignIcon() => Headless.Run(() =>
    {
        var icon = CreateIcon();
        var host = new ContentControl { Content = icon };

        var window = new Window { Content = host };
        window.Styles.Add(new Style(selector => selector.OfType<PathIcon>())
        {
            Setters = { new Setter(PathIcon.ForegroundProperty, Brushes.Red) },
        });
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(Colors.Red, ColorOf(icon.Foreground));
    });

    /// <summary>
    /// The copy/reveal feedback swaps button icons at runtime, so the icons it
    /// relies on must stay available in the AtomUI catalog across upgrades.
    /// (AtomUI icons draw through DrawingInstructions and ignore PathIcon.Data,
    /// so there is no geometry property to assert here.)
    /// </summary>
    [Fact]
    public void CopyFeedbackIconsExistInCatalog()
    {
        var names = AntDesignIconCatalog.GetIcons()
            .Select(candidate => candidate.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CopyOutlined", names);
        Assert.Contains("CheckOutlined", names);
        Assert.Contains("EyeOutlined", names);
        Assert.Contains("EyeInvisibleOutlined", names);
    }

    private static Color? ColorOf(IBrush? brush) => brush switch
    {
        SolidColorBrush solid => solid.Color,
        ImmutableSolidColorBrush immutable => immutable.Color,
        _ => null,
    };

    private static PathIcon CreateIcon()
        => (PathIcon)AntDesignIconCatalog.GetIcons().First(candidate => candidate.Name == "StarFilled").Creator();
}
