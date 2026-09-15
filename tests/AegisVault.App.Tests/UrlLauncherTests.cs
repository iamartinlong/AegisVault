using AegisVault.App.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class UrlLauncherTests
{
    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com/login", true)]
    [InlineData("HTTPS://EXAMPLE.COM", true)]
    [InlineData("  https://example.com  ", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("example.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsSupportedValidatesScheme(string? url, bool expected)
        => Assert.Equal(expected, SystemUrlLauncher.Instance.IsSupported(url));

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("example.com")]
    [InlineData(null)]
    public void TryOpenRefusesUnsupportedUrls(string? url)
        => Assert.False(SystemUrlLauncher.Instance.TryOpen(url));
}
