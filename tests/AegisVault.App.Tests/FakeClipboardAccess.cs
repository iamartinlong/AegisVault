using AegisVault.App.Services;

namespace AegisVault.App.Tests;

internal sealed class FakeClipboardAccess : IClipboardAccess
{
    public string? Text { get; set; }

    public int SetCount { get; private set; }

    public int ClearCount { get; private set; }

    public Task SetTextAsync(string text)
    {
        Text = text;
        SetCount++;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync() => Task.FromResult(Text);

    public Task ClearAsync()
    {
        Text = null;
        ClearCount++;
        return Task.CompletedTask;
    }
}
