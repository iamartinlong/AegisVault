using AegisVault.Platform;
using Xunit;

namespace AegisVault.Platform.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void SecondAcquirerIsNotTheOwner()
    {
        using var first = SingleInstanceGuard.Acquire();
        Assert.True(first.IsOwner);

        // The second attempt must run on a *different* thread: a named mutex
        // is owned per thread, and Task.Run may reuse the awaiting thread
        // (recursive acquisition would look like success).
        using var second = AcquireOnADedicatedThread();
        Assert.False(second.IsOwner);
    }

    [Fact]
    public void GuardCanBeReacquiredAfterRelease()
    {
        var first = SingleInstanceGuard.Acquire();
        Assert.True(first.IsOwner);
        first.Dispose();

        using var second = AcquireOnADedicatedThread();
        Assert.True(second.IsOwner);
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

    [Fact]
    public void ReleaseHandsTheLockOverForARestart()
    {
        using var first = SingleInstanceGuard.Acquire();
        Assert.True(first.IsOwner);

        // The restart flow releases before spawning the replacement process.
        Assert.True(first.Release());
        Assert.False(first.IsOwner);

        using var second = AcquireOnADedicatedThread();
        Assert.True(second.IsOwner);
    }

    [Fact]
    public void ReacquireSucceedsWhenNobodyTookOver()
    {
        using var guard = SingleInstanceGuard.Acquire();

        Assert.True(guard.Release());
        Assert.True(guard.TryReacquire());
        Assert.True(guard.IsOwner);
    }

    [Fact]
    public void ReacquireFailsWhenAnotherInstanceTookOver()
    {
        using var guard = SingleInstanceGuard.Acquire();
        Assert.True(guard.Release());

        // The other instance must stay alive while it owns the lock: a thread
        // that exits would abandon the mutex, and an abandoned mutex may be
        // taken over by anyone (the guard treats that as ownership).
        var (other, stop, worker) = AcquireOnALiveThread();
        try
        {
            Assert.True(other.IsOwner);
            Assert.False(guard.TryReacquire());
            Assert.False(guard.IsOwner);
        }
        finally
        {
            stop.Set();
            worker.Join();
        }
    }

    /// <summary>
    /// Acquires on a dedicated thread, then waits for it to finish. Task.Run
    /// is not enough: the pool may reuse the awaiting thread and a named
    /// mutex is owned per thread.
    /// </summary>
    private static SingleInstanceGuard AcquireOnADedicatedThread()
    {
        SingleInstanceGuard? guard = null;
        var thread = new Thread(() => guard = SingleInstanceGuard.Acquire())
        {
            IsBackground = true,
        };

        thread.Start();
        thread.Join();
        return guard!;
    }

    /// <summary>
    /// Acquires on a dedicated thread that keeps owning the lock until the
    /// returned <see cref="ManualResetEventSlim"/> is set.
    /// </summary>
    private static (SingleInstanceGuard Guard, ManualResetEventSlim Stop, Thread Thread) AcquireOnALiveThread()
    {
        SingleInstanceGuard? guard = null;
        var stop = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            guard = SingleInstanceGuard.Acquire();
            stop.Wait();
        })
        {
            IsBackground = true,
        };

        thread.Start();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref guard) is not null, TimeSpan.FromSeconds(5)));
        return (guard!, stop, thread);
    }
}
