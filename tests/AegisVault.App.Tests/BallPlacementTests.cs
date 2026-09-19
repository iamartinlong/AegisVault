using System.Globalization;
using Avalonia;
using AegisVault.App.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class BallPlacementTests
{
    [Fact]
    public void ParsingIsCultureInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // A culture with non-ASCII digits must not change how the stored
            // placement is parsed.
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            Assert.True(BallPlacement.TryParse("12,34", out var position));
            Assert.Equal(new PixelPoint(12, 34), position);

            Assert.False(BallPlacement.TryParse("١٢,٣٤", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("120,340", 120, 340)]
    [InlineData("-40,  12 ", -40, 12)]
    public void ParsesStoredPositions(string value, int x, int y)
    {
        Assert.True(BallPlacement.TryParse(value, out var position));
        Assert.Equal(new PixelPoint(x, y), position);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("120")]
    [InlineData("a,b")]
    [InlineData("120,340,560")]
    public void RejectsMalformedPositions(string? value)
        => Assert.False(BallPlacement.TryParse(value, out _));

    [Fact]
    public void FormatRoundTrips()
    {
        var position = new PixelPoint(-12, 34);
        Assert.True(BallPlacement.TryParse(BallPlacement.Format(position), out var parsed));
        Assert.Equal(position, parsed);
    }

    [Fact]
    public void ClampKeepsTheBallInsideTheWorkArea()
    {
        var area = new PixelRect(0, 0, 1920, 1040);

        Assert.Equal(new PixelPoint(0, 0), BallPlacement.Clamp(new PixelPoint(-80, -30), area, 56, 56));
        Assert.Equal(new PixelPoint(1864, 984), BallPlacement.Clamp(new PixelPoint(5000, 5000), area, 56, 56));
        Assert.Equal(new PixelPoint(700, 400), BallPlacement.Clamp(new PixelPoint(700, 400), area, 56, 56));
    }

    [Fact]
    public void ClampHandlesWorkAreasSmallerThanTheBall()
    {
        var area = new PixelRect(100, 100, 20, 20);
        Assert.Equal(new PixelPoint(100, 100), BallPlacement.Clamp(new PixelPoint(500, 500), area, 56, 56));
    }

    [Theory]
    [InlineData("left", "left")]
    [InlineData("right", "right")]
    [InlineData("top", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizesDockSides(string? value, string? expected)
        => Assert.Equal(expected, BallPlacement.NormalizeSide(value));
}
