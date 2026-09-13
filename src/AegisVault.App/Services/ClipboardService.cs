using System.Security.Cryptography;
using System.Text;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Avalonia.Threading;

namespace AegisVault.App.Services;

/// <summary>
/// Copies secrets to the clipboard and removes them again after the configured
/// delay, but only when the clipboard still contains exactly what we copied.
/// Only a SHA-256 fingerprint of the copied secret is retained in memory.
/// </summary>
public sealed class ClipboardService : IDisposable
{
    private readonly IClipboardAccess _clipboard;
    private readonly Func<UserConfig> _configProvider;
    private readonly DispatcherTimer _timer;

    private string? _lastCopiedHash;
    private bool _disposed;

    public ClipboardService(IClipboardAccess clipboard, Func<UserConfig> configProvider)
    {
        _clipboard = clipboard;
        _configProvider = configProvider;
        _timer = new DispatcherTimer();
        _timer.Tick += OnTimerTick;
    }

    public async Task CopyAsync(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return;
        }

        await _clipboard.SetTextAsync(secret);
        _lastCopiedHash = ComputeHash(secret);

        var delay = ClipboardPolicy.GetClearDelay(_configProvider());
        _timer.Stop();
        if (delay != Timeout.InfiniteTimeSpan)
        {
            _timer.Interval = delay;
            _timer.Start();
        }
    }

    /// <summary>Clears the clipboard if it still holds the last copied secret.</summary>
    public async Task ClearIfUnchangedAsync()
    {
        var current = await _clipboard.GetTextAsync();
        var currentHash = current is null ? null : ComputeHash(current);
        if (ClipboardPolicy.ShouldClear(currentHash, _lastCopiedHash))
        {
            await _clipboard.ClearAsync();
        }

        _lastCopiedHash = null;
        _timer.Stop();
    }

    /// <summary>
    /// Stops the clear timer. When a secret was copied and not yet cleared,
    /// a best-effort clear is issued immediately (e.g. on vault lock or exit).
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();

        if (_lastCopiedHash is not null)
        {
            ClearQuietly();
        }
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            await ClearIfUnchangedAsync();
        }
        catch (Exception)
        {
        }
    }

    private async void ClearQuietly()
    {
        try
        {
            await ClearIfUnchangedAsync();
        }
        catch (Exception)
        {
        }
    }

    private static string ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
