using AegisVault.App.Services;
using AegisVault.App.ViewModels;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class ClipboardToastTests
{
    [Fact]
    public Task CopyStartsToastWithRemainingSeconds() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var vault = CreateMainVault();
        using var clipboard = new ClipboardService(fake, () => new UserConfig { ClipboardClearSeconds = 30 });
        using var viewModel = new MainViewModel(vault, clipboard);

        await clipboard.CopyAsync("s3cret");

        Assert.True(viewModel.IsClipboardToastVisible);
        Assert.Equal(30, viewModel.ClipboardToastRemaining);
        Assert.Contains("30", viewModel.ClipboardToastText);
        return null;
    });

    [Fact]
    public Task ClearNowHidesToastAndClearsClipboard() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var vault = CreateMainVault();
        using var clipboard = new ClipboardService(fake, () => new UserConfig { ClipboardClearSeconds = 30 });
        using var viewModel = new MainViewModel(vault, clipboard);

        await clipboard.CopyAsync("s3cret");
        Assert.True(viewModel.IsClipboardToastVisible);

        await viewModel.ClearClipboardNowCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsClipboardToastVisible);
        Assert.Null(fake.Text);
        Assert.Equal(1, fake.ClearCount);
        return null;
    });

    [Fact]
    public Task ZeroDelayCopyShowsNoToast() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var vault = CreateMainVault();
        using var clipboard = new ClipboardService(fake, () => new UserConfig { ClipboardClearSeconds = 0 });
        using var viewModel = new MainViewModel(vault, clipboard);

        await clipboard.CopyAsync("s3cret");

        Assert.False(viewModel.IsClipboardToastVisible);
        return null;
    });

    private static VaultService CreateMainVault()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aegis-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var vault = VaultService.CreateNew(
            Path.Combine(directory, "vault.aegis"),
            "master password"u8);
        return vault;
    }
}
