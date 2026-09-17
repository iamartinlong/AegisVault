using System.Threading;

namespace AegisVault.Platform;

/// <summary>
/// Process-level guard: only one AegisVault instance may own the vault at a
/// time. A second launch activates the running instance's window and exits
/// instead of starting another process (two instances would double the tray
/// icon and floating ball, lose the second hotkey registration, and write the
/// same vault from two stale views).
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>Session-scoped name (per Windows session, so RDP users do not collide).</summary>
    public const string WindowsMutexName = @"Local\AegisVault.SingleInstance";

    private readonly Mutex _mutex;
    private bool _owned;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, bool isOwner)
    {
        _mutex = mutex;
        _owned = isOwner;
    }

    /// <summary>True when this process currently owns the instance lock.</summary>
    public bool IsOwner => _owned;

    /// <summary>
    /// Takes the instance mutex. The returned guard must stay alive for the
    /// whole process lifetime (release happens on dispose/exit).
    /// </summary>
    public static SingleInstanceGuard Acquire()
    {
        var name = OperatingSystem.IsWindows()
            ? WindowsMutexName
            : "AegisVault.SingleInstance";

        var mutex = new Mutex(initiallyOwned: false, name, out _);
        bool isOwner;
        try
        {
            isOwner = mutex.WaitOne(TimeSpan.Zero, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner crashed without releasing; the vault is ours.
            isOwner = true;
        }

        return new SingleInstanceGuard(mutex, isOwner);
    }

    /// <summary>
    /// Gives up ownership while keeping the handle, so a freshly spawned
    /// replacement process can take over (restart hand-off). Returns false when
    /// this guard no longer owns the lock.
    /// </summary>
    public bool Release()
    {
        if (_disposed || !_owned)
        {
            return false;
        }

        try
        {
            _mutex.ReleaseMutex();
            _owned = false;
            return true;
        }
        catch (ApplicationException)
        {
            return false;
        }
    }

    /// <summary>Re-takes the lock after a failed hand-off (same thread).</summary>
    public bool TryReacquire()
    {
        if (_disposed || _owned)
        {
            return _owned;
        }

        try
        {
            _owned = _mutex.WaitOne(TimeSpan.Zero, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            _owned = true;
        }

        return _owned;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_owned)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not the owner anymore (should not happen); ignore.
            }

            _owned = false;
        }

        _mutex.Dispose();
    }
}
