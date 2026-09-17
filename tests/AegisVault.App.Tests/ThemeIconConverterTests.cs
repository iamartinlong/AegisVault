using System.Linq;
using AegisVault.App.Converters;
using AtomUI.Controls;
using Xunit;

namespace AegisVault.App.Tests;

/// <summary>
/// The toolbar theme button must show the active mode (sun/moon/desktop) and
/// never share an icon instance: a reused icon would be attached to a second
/// parent and throw (pitfall P-10).
/// </summary>
public sealed class ThemeIconConverterTests
{
    private static Icon Convert(string? preference)
    {
        var icon = new ThemeIconConverter().Convert(preference, typeof(Icon), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(icon);
        return (Icon)icon!;
    }

    [Fact]
    public void EachModeUsesItsOwnIcon()
    {
        var system = Convert("system");
        var light = Convert("light");
        var dark = Convert("dark");

        Assert.Equal(3, new[] { system.GetType(), light.GetType(), dark.GetType() }.Distinct().Count());
    }

    [Fact]
    public void UnknownPreferenceFallsBackToTheSystemIcon()
        => Assert.Equal(Convert("system").GetType(), Convert("garbage").GetType());

    [Fact]
    public void EveryCallReturnsAFreshInstance()
    {
        Assert.NotSame(Convert("light"), Convert("light"));
        Assert.NotSame(Convert("dark"), Convert("dark"));
    }
}
