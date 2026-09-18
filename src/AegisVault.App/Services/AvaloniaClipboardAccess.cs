using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using AegisVault.Platform;

namespace AegisVault.App.Services;

/// <summary>Clipboard access bound to the currently active top-level window.</summary>
public sealed class AvaloniaClipboardAccess : IClipboardAccess
{
    private readonly Func<TopLevel?> _topLevelProvider;

    public AvaloniaClipboardAccess(Func<TopLevel?> topLevelProvider)
    {
        _topLevelProvider = topLevelProvider;
    }

    public async Task SetTextAsync(string text)
    {
        if (GetClipboard() is not { } clipboard)
        {
            return;
        }

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(text));
        await clipboard.SetDataAsync(transfer);

        // Keep secrets out of the Windows clipboard history and cloud
        // clipboard while this process still owns the clipboard.
        var handle = _topLevelProvider()?.TryGetPlatformHandle()?.Handle ?? 0;
        ClipboardExclusion.TryMarkCurrent(handle);
    }

    public async Task<string?> GetTextAsync()
    {
        if (GetClipboard() is not { } clipboard)
        {
            return null;
        }

        var transfer = await clipboard.TryGetDataAsync();
        if (transfer is null)
        {
            return null;
        }

        try
        {
            return await transfer.TryGetTextAsync();
        }
        finally
        {
            transfer.Dispose();
        }
    }

    public Task ClearAsync()
        => GetClipboard()?.ClearAsync() ?? Task.CompletedTask;

    private IClipboard? GetClipboard() => _topLevelProvider()?.Clipboard;
}
