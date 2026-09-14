using AegisVault.App.ViewModels;
using AegisVault.Platform;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class LockViewModelTests
{
    [Fact]
    public void UnlockCommandRaisesUnlockRequested()
    {
        var viewModel = new LockViewModel();
        var raised = false;
        viewModel.UnlockRequested += () => raised = true;

        viewModel.UnlockCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ScreenCaptureGuardRejectsInvalidHandle()
    {
        // Even on Windows an invalid handle must fail gracefully.
        Assert.False(ScreenCaptureGuard.TrySetExcluded(0, true));
    }

    [Fact]
    public void HotKeyServiceWithoutRegistrationDisposesCleanly()
    {
        using var service = new HotKeyService();
        Assert.Equal(OperatingSystem.IsWindows(), service.IsSupported);
        // Dispose without registration must be idempotent and not throw.
        service.Dispose();
        service.Dispose();
    }
}
