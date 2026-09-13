using AegisVault.App.Services;
using AegisVault.Core.Models;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class ClipboardServiceTests
{
    private static UserConfig Config(int seconds = 30) => new() { ClipboardClearSeconds = seconds };

    [Fact]
    public Task CopyThenClearWhenUnchanged() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var service = new ClipboardService(fake, () => Config());

        await service.CopyAsync("s3cret");
        Assert.Equal("s3cret", fake.Text);

        await service.ClearIfUnchangedAsync();
        Assert.Null(fake.Text);
        Assert.Equal(1, fake.ClearCount);
        return null;
    });

    [Fact]
    public Task DoesNotClearWhenClipboardWasReplaced() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var service = new ClipboardService(fake, () => Config());

        await service.CopyAsync("s3cret");
        fake.Text = "user replaced it";

        await service.ClearIfUnchangedAsync();
        Assert.Equal("user replaced it", fake.Text);
        Assert.Equal(0, fake.ClearCount);
        return null;
    });

    [Fact]
    public Task EmptySecretDoesNothing() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var service = new ClipboardService(fake, () => Config());

        await service.CopyAsync(string.Empty);
        await service.CopyAsync(null);

        Assert.Equal(0, fake.SetCount);
        return null;
    });

    [Fact]
    public Task ZeroDelayDisablesAutoClear() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var service = new ClipboardService(fake, () => Config(0));

        await service.CopyAsync("s3cret");

        Assert.Equal("s3cret", fake.Text);
        return null;
    });

    [Fact]
    public Task DisposeClearsClipboardWhenUnchanged() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        var service = new ClipboardService(fake, () => Config());

        await service.CopyAsync("s3cret");
        Assert.Equal("s3cret", fake.Text);

        service.Dispose();

        Assert.Null(fake.Text);
        return null;
    });

    [Fact]
    public Task DisposeKeepsClipboardWhenReplacedByUser() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        var service = new ClipboardService(fake, () => Config());

        await service.CopyAsync("s3cret");
        fake.Text = "user content";

        service.Dispose();

        Assert.Equal("user content", fake.Text);
        return null;
    });
}
