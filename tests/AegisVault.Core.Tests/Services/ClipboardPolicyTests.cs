using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class ClipboardPolicyTests
{
    [Fact]
    public void ClearsOnlyWhenClipboardStillContainsCopiedValue()
    {
        Assert.True(ClipboardPolicy.ShouldClear("secret-value", "secret-value"));
        Assert.False(ClipboardPolicy.ShouldClear("something else", "secret-value"));
        Assert.False(ClipboardPolicy.ShouldClear(null, "secret-value"));
        Assert.False(ClipboardPolicy.ShouldClear("", "secret-value"));
        Assert.False(ClipboardPolicy.ShouldClear("secret-value", null));
    }

    [Fact]
    public void DelayComesFromConfiguration()
    {
        var config = new UserConfig { ClipboardClearSeconds = 45 };

        Assert.Equal(TimeSpan.FromSeconds(45), ClipboardPolicy.GetClearDelay(config));
    }

    [Fact]
    public void ZeroOrNegativeDelayDisablesClearing()
    {
        Assert.Equal(Timeout.InfiniteTimeSpan, ClipboardPolicy.GetClearDelay(new UserConfig { ClipboardClearSeconds = 0 }));
        Assert.Equal(Timeout.InfiniteTimeSpan, ClipboardPolicy.GetClearDelay(new UserConfig { ClipboardClearSeconds = -5 }));
    }
}
