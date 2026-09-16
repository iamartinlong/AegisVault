using AegisVault.App.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class WebUrlTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("example.com", "https://example.com")]
    [InlineData("  example.com/path  ", "https://example.com/path")]
    [InlineData("https://example.com/login", "https://example.com/login")]
    [InlineData("HTTP://EXAMPLE.COM", "HTTP://EXAMPLE.COM")]
    [InlineData("localhost:5000", "https://localhost:5000")]
    [InlineData("127.0.0.1:8080", "https://127.0.0.1:8080")]
    public void NormalizesSupportedAddresses(string? input, string expected)
        => Assert.Equal(expected, WebUrl.Normalize(input));

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-url")]
    [InlineData("https://")]
    [InlineData("https://intranet")]
    public void RejectsUnsupportedAddresses(string input)
        => Assert.Null(WebUrl.Normalize(input));
}
