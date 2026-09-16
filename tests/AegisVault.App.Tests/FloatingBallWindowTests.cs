using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using AegisVault.App.Views;
using Xunit;

namespace AegisVault.App.Tests;

/// <summary>
/// The floating ball used to swallow every pointer event (a Button marked
/// PointerPressed as handled), so click and drag silently did nothing.
/// </summary>
public sealed class FloatingBallWindowTests
{
    private static readonly Point BallCentre = new(26, 26);

    [Fact]
    public Task ClickRaisesBallClicked() => Headless.Run(() =>
    {
        var window = new FloatingBallWindow();
        try
        {
            window.Show();
            var raised = 0;
            window.BallClicked += () => raised++;

            window.MouseDown(BallCentre, MouseButton.Left);
            window.MouseUp(BallCentre, MouseButton.Left);

            Assert.Equal(1, raised);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task DragMovesTheWindowAndDoesNotClick() => Headless.Run(() =>
    {
        var window = new FloatingBallWindow();
        try
        {
            window.Show();
            var raised = 0;
            window.BallClicked += () => raised++;

            window.MouseDown(BallCentre, MouseButton.Left);
            window.MouseMove(new Point(BallCentre.X + 60, BallCentre.Y + 40));
            window.MouseUp(new Point(BallCentre.X + 60, BallCentre.Y + 40), MouseButton.Left);

            Assert.Equal(0, raised);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task TinyJitterStillCountsAsClick() => Headless.Run(() =>
    {
        var window = new FloatingBallWindow();
        try
        {
            window.Show();
            var raised = 0;
            window.BallClicked += () => raised++;

            window.MouseDown(BallCentre, MouseButton.Left);
            window.MouseMove(new Point(BallCentre.X + 1, BallCentre.Y + 1));
            window.MouseUp(new Point(BallCentre.X + 1, BallCentre.Y + 1), MouseButton.Left);

            Assert.Equal(1, raised);
        }
        finally
        {
            window.Close();
        }
    });
}
