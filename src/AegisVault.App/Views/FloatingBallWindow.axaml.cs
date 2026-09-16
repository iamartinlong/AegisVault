using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace AegisVault.App.Views;

/// <summary>
/// Optional always-on-top floating ball: click opens quick access, drag moves
/// it around the screen. Dragging is handled manually (a press that never
/// leaves the window counts as a click), because the platform move loop gives
/// no reliable way to tell "clicked" from "dragged".
/// </summary>
public partial class FloatingBallWindow : Window
{
    private const int DragThreshold = 4;

    /// <summary>Raised on a plain click (no drag).</summary>
    public event Action? BallClicked;

    private PixelPoint _pressScreen;
    private PixelPoint _pressWindow;
    private bool _dragging;
    private bool _moved;

    public FloatingBallWindow()
    {
        InitializeComponent();
    }

    private void OnBallPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressScreen = this.PointToScreen(e.GetPosition(this));
        _pressWindow = Position;
        _dragging = true;
        _moved = false;
        BallSurface.Classes.Add("pressed");
        e.Pointer.Capture(BallSurface);
        e.Handled = true;
    }

    private void OnBallPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var current = this.PointToScreen(e.GetPosition(this));
        var dx = current.X - _pressScreen.X;
        var dy = current.Y - _pressScreen.Y;
        if (Math.Abs(dx) + Math.Abs(dy) < DragThreshold)
        {
            return;
        }

        _moved = true;
        Position = new PixelPoint(_pressWindow.X + dx, _pressWindow.Y + dy);
        e.Handled = true;
    }

    private void OnBallPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        BallSurface.Classes.Remove("pressed");
        e.Pointer.Capture(null);
        if (!_moved)
        {
            BallClicked?.Invoke();
        }

        e.Handled = true;
    }

    private void OnBallPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _dragging = false;
        BallSurface.Classes.Remove("pressed");
    }
}
