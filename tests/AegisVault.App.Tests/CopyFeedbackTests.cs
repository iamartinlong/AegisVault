using AtomUI.Icons.AntDesign;
using Avalonia.Controls;
using Avalonia.Media;
using AegisVault.App.Services;
using Xunit;
// AtomUI's button (the XAML `atom:Button`) is the one that owns an Icon property.
using Button = AtomUI.Desktop.Controls.Button;

namespace AegisVault.App.Tests;

public sealed class CopyFeedbackTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(60);

    [Fact]
    public Task FlashSwapsIconThenRestoresIt() => Headless.RunAsync<object?>(async () =>
    {
        var originalIcon = new CopyOutlined();
        var originalBrush = Brushes.Red;
        var button = new Button { Icon = originalIcon, Foreground = originalBrush };

        var flash = CopyFeedback.FlashAsync(button, duration: Short);

        // The check mark is applied before the first await.
        Assert.IsType<CheckOutlined>(button.Icon);
        Assert.NotSame(originalBrush, button.Foreground);

        await flash;

        Assert.Same(originalIcon, button.Icon);
        Assert.Same(originalBrush, button.Foreground);
        return null;
    });

    [Fact]
    public Task RepeatedFlashRestoresOnce() => Headless.RunAsync<object?>(async () =>
    {
        var originalIcon = new CopyOutlined();
        var button = new Button { Icon = originalIcon };

        var first = CopyFeedback.FlashAsync(button, duration: Short);
        var second = CopyFeedback.FlashAsync(button, duration: Short);

        await first;
        await second;

        Assert.Same(originalIcon, button.Icon);
        return null;
    });

    [Fact]
    public Task CancelledFlashKeepsTheCheckMark() => Headless.RunAsync<object?>(async () =>
    {
        var button = new Button { Icon = new CopyOutlined() };

        using var cancellation = new CancellationTokenSource();
        var flash = CopyFeedback.FlashAsync(button, duration: TimeSpan.FromMilliseconds(80), cancellationToken: cancellation.Token);
        cancellation.Cancel();

        await flash;

        // The pending restore was cancelled; a later flash owns the button.
        Assert.IsType<CheckOutlined>(button.Icon);
        return null;
    });

    [Fact]
    public void FlashIgnoresNullButton() => CopyFeedback.Flash(null);
}
