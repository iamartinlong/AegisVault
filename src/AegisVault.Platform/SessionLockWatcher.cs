using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AegisVault.Platform;

/// <summary>
/// Watches OS session events (screen lock, suspend) so the vault can lock
/// immediately. Only supported on Windows; other platforms stay inert.
/// </summary>
public sealed class SessionLockWatcher : IDisposable
{
    private bool _subscribed;

    public SessionLockWatcher()
    {
        if (OperatingSystem.IsWindows())
        {
            Subscribe();
        }
    }

    public event Action? ScreenLocked;

    public event Action? Suspended;

    public bool IsSupported => _subscribed;

    public void Dispose()
    {
        if (!_subscribed)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Unsubscribe();
        }

        _subscribed = false;
    }

    [SupportedOSPlatform("windows")]
    private void Subscribe()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _subscribed = true;
    }

    [SupportedOSPlatform("windows")]
    private void Unsubscribe()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    [SupportedOSPlatform("windows")]
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            ScreenLocked?.Invoke();
        }
    }

    [SupportedOSPlatform("windows")]
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            Suspended?.Invoke();
        }
    }
}
