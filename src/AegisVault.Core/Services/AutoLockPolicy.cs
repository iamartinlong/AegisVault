using AegisVault.Core.Models;

namespace AegisVault.Core.Services;

public enum AutoLockTrigger
{
    None,
    Idle,
    Minimize,
    ScreenLock,
    Suspend,
}

/// <summary>
/// Pure decision logic for automatic vault locking. Platform watchers feed
/// state into <see cref="Evaluate"/>; the result is acted upon by the UI layer.
/// </summary>
public static class AutoLockPolicy
{
    public static AutoLockTrigger Evaluate(
        UserConfig config,
        TimeSpan idleFor,
        bool minimized = false,
        bool screenLocked = false,
        bool suspended = false)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (suspended && config.LockOnSuspend)
        {
            return AutoLockTrigger.Suspend;
        }

        if (screenLocked && config.LockOnScreenLock)
        {
            return AutoLockTrigger.ScreenLock;
        }

        if (minimized && config.LockOnMinimize)
        {
            return AutoLockTrigger.Minimize;
        }

        if (config.AutoLockMinutes > 0 && idleFor >= TimeSpan.FromMinutes(config.AutoLockMinutes))
        {
            return AutoLockTrigger.Idle;
        }

        return AutoLockTrigger.None;
    }
}
