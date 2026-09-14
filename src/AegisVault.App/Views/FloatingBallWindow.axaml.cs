using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AegisVault.App.Views;

/// <summary>
/// Optional always-on-top floating ball: click opens quick access, drag moves
/// it around the screen.
/// </summary>
public partial class FloatingBallWindow : Window
{
    /// <summary>Raised on a plain click (no drag).</summary>
    public event Action? BallClicked;

    private Point _dragStart;
    private bool _dragging;
    private bool _moved;

    public FloatingBallWindow()
    {
        InitializeComponent();
    }

    private void OnBallPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragging = true;
            _moved = false;
            _dragStart = e.GetPosition(this);
            e.Pointer.Capture(BallButton);
            e.Handled = true;
        }
    }

    private void OnBallPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var delta = e.GetPosition(this) - _dragStart;
        if (delta.X * delta.X + delta.Y * delta.Y < 9)
        {
            return;
        }

        _moved = true;
        Position = new PixelPoint(
            (int)Math.Round(Position.X + delta.X),
            (int)Math.Round(Position.Y + delta.Y));
        e.Handled = true;
    }

    private void OnBallPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        if (!_moved)
        {
            BallClicked?.Invoke();
        }

        e.Handled = true;
    }
}
