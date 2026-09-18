using AegisVault.Platform;
using Xunit;

namespace AegisVault.Platform.Tests;

public sealed class ClipboardExclusionTests
{
    [Fact]
    public void RejectsZeroHandle()
    {
        Assert.False(ClipboardExclusion.TryMarkCurrent(0));
    }

    [Fact]
    public void ReportsUnsupportedPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.False(ClipboardExclusion.IsSupported);
        Assert.False(ClipboardExclusion.TryMarkCurrent(1234));
    }

    [Fact]
    public void InvalidWindowFailsWithoutThrowing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.True(ClipboardExclusion.IsSupported);
        Assert.False(ClipboardExclusion.TryMarkCurrent(1234));
    }
}
