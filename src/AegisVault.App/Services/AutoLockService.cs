using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Avalonia.Threading;

namespace AegisVault.App.Services;

/// <summary>
/// Periodically evaluates <see cref="AutoLockPolicy"/> against user activity
/// and window state, raising <see cref="LockTriggered"/> when locking is due.
/// </summary>
public sealed class AutoLockService : IDisposable
{
    private readonly Func<UserConfig> _configProvider;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherTimer _timer;

    private DateTimeOffset _lastActivity;
    private bool _minimized;
    private bool _disposed;

    public event Action<AutoLockTrigger>? LockTriggered;

    public AutoLockService(Func<UserConfig> configProvider, TimeProvider? timeProvider = null)
    {
        _configProvider = configProvider;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastActivity = _timeProvider.GetUtcNow();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timer.Tick += (_, _) => Evaluate();
        _timer.Start();
    }

    /// <summary>Resets the idle clock; also clears the minimized state.</summary>
    public void ReportActivity()
    {
        _lastActivity = _timeProvider.GetUtcNow();
        _minimized = false;
    }

    public void SetMinimized(bool minimized)
    {
        _minimized = minimized;
        if (minimized)
        {
            Evaluate();
        }
    }

    public void ReportScreenLocked() => Evaluate(screenLocked: true);

    public void ReportSuspended() => Evaluate(suspended: true);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
    }

    private void Evaluate(bool screenLocked = false, bool suspended = false)
    {
        if (_disposed)
        {
            return;
        }

        UserConfig config;
        try
        {
            config = _configProvider();
        }
        catch (Exception)
        {
            return;
        }

        var trigger = AutoLockPolicy.Evaluate(
            config,
            _timeProvider.GetUtcNow() - _lastActivity,
            minimized: _minimized,
            screenLocked: screenLocked,
            suspended: suspended);

        if (trigger == AutoLockTrigger.None)
        {
            return;
        }

        _timer.Stop();
        LockTriggered?.Invoke(trigger);
    }
}
