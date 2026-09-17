using AegisVault.Platform;
using Xunit;

namespace AegisVault.Platform.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public async Task SecondAcquirerIsNotTheOwner()
    {
        using var first = SingleInstanceGuard.Acquire();
        Assert.True(first.IsOwner);

        // A mutex is owned per thread: the second attempt comes from another
        // thread, which is exactly the "second process" situation.
        var second = await Task.Run(SingleInstanceGuard.Acquire);
        try
        {
            Assert.False(second.IsOwner);
        }
        finally
        {
            second.Dispose();
        }
    }

    [Fact]
    public async Task GuardCanBeReacquiredAfterRelease()
    {
        var first = SingleInstanceGuard.Acquire();
        Assert.True(first.IsOwner);
        first.Dispose();

        var second = await Task.Run(SingleInstanceGuard.Acquire);
        try
        {
            Assert.True(second.IsOwner);
        }
        finally
        {
            second.Dispose();
        }
    }

    [Fact]
    public void TitleMatchingIgnoresPaddingAndCase()
    {
        Assert.True(WindowActivation.IsTitleMatch("  AegisVault ", "AegisVault"));
        Assert.True(WindowActivation.IsTitleMatch("AegisVault", "AegisVault"));
        Assert.False(WindowActivation.IsTitleMatch("AegisVaultBall", "AegisVault"));
        Assert.False(WindowActivation.IsTitleMatch("aegisvault", "AegisVault"));
        Assert.False(WindowActivation.IsTitleMatch(null, "AegisVault"));
        Assert.False(WindowActivation.IsTitleMatch("   ", "AegisVault"));
    }

    [Fact]
    public void ActivatingAnUnknownTitleFails()
    {
        var activated = WindowActivation.TryActivateByTitle("AegisVault-No-Such-Window-Title");

        Assert.False(activated);
    }
}
