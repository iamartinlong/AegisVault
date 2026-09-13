using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class AutoLockPolicyTests
{
    private static readonly UserConfig Defaults = new();

    [Fact]
    public void NoTriggerWhenNothingApplies()
    {
        var trigger = AutoLockPolicy.Evaluate(Defaults, TimeSpan.FromMinutes(1));

        Assert.Equal(AutoLockTrigger.None, trigger);
    }

    [Fact]
    public void IdleTriggersAtConfiguredThreshold()
    {
        var config = Defaults with { AutoLockMinutes = 5 };

        Assert.Equal(AutoLockTrigger.None, AutoLockPolicy.Evaluate(config, TimeSpan.FromMinutes(4.9)));
        Assert.Equal(AutoLockTrigger.Idle, AutoLockPolicy.Evaluate(config, TimeSpan.FromMinutes(5)));
        Assert.Equal(AutoLockTrigger.Idle, AutoLockPolicy.Evaluate(config, TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void IdleDisabledWhenMinutesIsZero()
    {
        var config = Defaults with { AutoLockMinutes = 0 };

        Assert.Equal(AutoLockTrigger.None, AutoLockPolicy.Evaluate(config, TimeSpan.FromDays(1)));
    }

    [Fact]
    public void MinimizeTriggersOnlyWhenEnabled()
    {
        Assert.Equal(
            AutoLockTrigger.None,
            AutoLockPolicy.Evaluate(Defaults with { LockOnMinimize = false }, TimeSpan.Zero, minimized: true));
        Assert.Equal(
            AutoLockTrigger.Minimize,
            AutoLockPolicy.Evaluate(Defaults with { LockOnMinimize = true }, TimeSpan.Zero, minimized: true));
    }

    [Fact]
    public void ScreenLockAndSuspendHonorConfiguration()
    {
        var config = Defaults with { LockOnScreenLock = true, LockOnSuspend = true };

        Assert.Equal(AutoLockTrigger.ScreenLock, AutoLockPolicy.Evaluate(config, TimeSpan.Zero, screenLocked: true));
        Assert.Equal(AutoLockTrigger.Suspend, AutoLockPolicy.Evaluate(config, TimeSpan.Zero, suspended: true));

        var relaxed = config with { LockOnScreenLock = false, LockOnSuspend = false };
        Assert.Equal(AutoLockTrigger.None, AutoLockPolicy.Evaluate(relaxed, TimeSpan.Zero, screenLocked: true));
        Assert.Equal(AutoLockTrigger.None, AutoLockPolicy.Evaluate(relaxed, TimeSpan.Zero, suspended: true));
    }

    [Fact]
    public void PrecedenceIsSuspendThenScreenLockThenMinimizeThenIdle()
    {
        var config = Defaults with
        {
            AutoLockMinutes = 1,
            LockOnMinimize = true,
            LockOnScreenLock = true,
            LockOnSuspend = true,
        };

        var idleFor = TimeSpan.FromMinutes(10);

        Assert.Equal(AutoLockTrigger.Suspend, AutoLockPolicy.Evaluate(config, idleFor, minimized: true, screenLocked: true, suspended: true));
        Assert.Equal(AutoLockTrigger.ScreenLock, AutoLockPolicy.Evaluate(config, idleFor, minimized: true, screenLocked: true));
        Assert.Equal(AutoLockTrigger.Minimize, AutoLockPolicy.Evaluate(config, idleFor, minimized: true));
        Assert.Equal(AutoLockTrigger.Idle, AutoLockPolicy.Evaluate(config, idleFor));
    }
}
