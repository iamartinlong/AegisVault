using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AegisVault.App.Controls;

/// <summary>
/// Shared 40px window title bar: brand/title on the left, self-drawn
/// minimize/maximize/close buttons on the right. Requires the host window to
/// extend its client area into the decorations
/// (<c>ExtendClientAreaToDecorationsHint</c> + <c>ExtendClientAreaTitleBarHeightHint=40</c>).
/// </summary>
public partial class AegisTitleBar : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AegisTitleBar, string?>(nameof(Title));

    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<AegisTitleBar, bool>(nameof(ShowIcon));

    public static readonly StyledProperty<bool> ShowMinimizeProperty =
        AvaloniaProperty.Register<AegisTitleBar, bool>(nameof(ShowMinimize));

    public static readonly StyledProperty<bool> ShowMaximizeProperty =
        AvaloniaProperty.Register<AegisTitleBar, bool>(nameof(ShowMaximize));

    public AegisTitleBar()
    {
        InitializeComponent();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowIcon
    {
        get => GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public bool ShowMinimize
    {
        get => GetValue(ShowMinimizeProperty);
        set => SetValue(ShowMinimizeProperty, value);
    }

    public bool ShowMaximize
    {
        get => GetValue(ShowMaximizeProperty);
        set => SetValue(ShowMaximizeProperty, value);
    }

    private Window? Host => TopLevel.GetTopLevel(this) as Window;

    private void OnStripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInteractive(e.Source) || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Host?.BeginMoveDrag(e);
    }

    private void OnStripDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsInteractive(e.Source))
        {
            return;
        }

        ToggleMaximize();
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        if (Host is { } host)
        {
            host.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Host?.Close();

    private void ToggleMaximize()
    {
        if (Host is not { CanResize: true } host)
        {
            return;
        }

        host.WindowState = host.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private bool IsInteractive(object? source)
    {
        for (var current = source as Visual; current is not null && current != this; current = current.GetVisualParent())
        {
            if (current is Button)
            {
                return true;
            }
        }

        return false;
    }
}
