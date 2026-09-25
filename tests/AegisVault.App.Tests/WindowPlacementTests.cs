using Avalonia;
using AegisVault.App.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class WindowPlacementTests
{
    [Fact]
    public void FormatRoundTrips()
    {
        var text = WindowPlacement.Format(new PixelPoint(120, 80), new PixelSize(1280, 800));

        Assert.True(WindowPlacement.TryParse(text, out var position, out var size));
        Assert.Equal(new PixelPoint(120, 80), position);
        Assert.Equal(new PixelSize(1280, 800), size);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("a,2,3,4")]
    [InlineData("1,2,3,x")]
    [InlineData("1,2,100,50")]      // too small to be usable
    [InlineData("1,2,-800,600")]    // negative size
    public void TryParseRejectsUnusableValues(string? text)
        => Assert.False(WindowPlacement.TryParse(text, out _, out _));

    [Fact]
    public void VisibleWhenOverlappingAScreen()
    {
        var screens = new[] { new Rect(0, 0, 1920, 1080) };

        Assert.True(WindowPlacement.IsVisibleOnScreens(new PixelPoint(100, 100), new PixelSize(900, 600), screens));
        Assert.True(WindowPlacement.IsVisibleOnScreens(new PixelPoint(1800, 1000), new PixelSize(900, 600), screens));
    }

    [Fact]
    public void InvisibleWhenOutsideEveryScreen()
    {
        var screens = new[] { new Rect(0, 0, 1920, 1080) };

        // A second monitor that was unplugged: the window is fully off-screen.
        Assert.False(WindowPlacement.IsVisibleOnScreens(new PixelPoint(2000, 100), new PixelSize(900, 600), screens));
        Assert.False(WindowPlacement.IsVisibleOnScreens(new PixelPoint(0, -700), new PixelSize(900, 600), screens));

        // Only a sliver left on screen is treated as lost too.
        Assert.False(WindowPlacement.IsVisibleOnScreens(new PixelPoint(-890, 100), new PixelSize(900, 600), screens));
        Assert.False(WindowPlacement.IsVisibleOnScreens(new PixelPoint(100, 1070), new PixelSize(900, 600), screens));
    }
}
