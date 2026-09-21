using System.Runtime.CompilerServices;
using AtomUI.Icons.AntDesign;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AegisVault.App.Theme;
// AtomUI's button (the XAML `atom:Button`) is the one that owns an Icon property.
using Button = AtomUI.Desktop.Controls.Button;

namespace AegisVault.App.Services;

/// <summary>
/// Transient "copied" feedback for copy buttons: the icon turns into a check mark
/// in the success colour and the original icon comes back after
/// <see cref="DefaultDuration"/>. Clicking again while the check mark is visible
/// restarts the timer instead of stacking restores.
///
/// AtomUI icons cannot be coloured through styles or inheritance (AGENTS rule 2),
/// so the colour goes on the button's <c>Foreground</c>, and the icon is replaced
/// with a freshly constructed instance — no reflection, so it stays AOT safe.
/// </summary>
public static class CopyFeedback
{
    /// <summary>How long the check mark stays visible.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(1500);

    private static readonly ConditionalWeakTable<Button, State> States = new();

    /// <summary>Fire-and-forget variant for click handlers.</summary>
    public static void Flash(Button? button)
    {
        if (button is not null)
        {
            _ = FlashAsync(button);
        }
    }

    /// <summary>
    /// Shows the check mark, waits, then restores the original icon and colour.
    /// The icon is swapped before the first await, so callers observe it
    /// immediately.
    /// </summary>
    public static async Task FlashAsync(
        Button button,
        TimeProvider? timeProvider = null,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(button);

        var state = States.GetOrCreateValue(button);

        state.Cancellation?.Cancel();
        state.Cancellation?.Dispose();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        state.Cancellation = cancellation;

        if (!state.Captured)
        {
            state.Icon = button.Icon;
            state.Foreground = button.Foreground;
            state.Captured = true;
        }

        button.Icon = CreateCheckIcon();
        button.Foreground = new SolidColorBrush(SuccessColor());

        try
        {
            await Task.Delay(duration ?? DefaultDuration, timeProvider ?? TimeProvider.System, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // A newer flash (or a closing window) owns the button now.
            return;
        }

        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        button.Icon = state.Icon;
        button.Foreground = state.Foreground;
        state.Captured = false;
        state.Icon = null;
        state.Foreground = null;
        state.Cancellation = null;
        cancellation.Dispose();
    }

    private static PathIcon CreateCheckIcon() => new CheckOutlined();

    private static Color SuccessColor()
        => AppTheme.TokenColor(
            "ColorSuccess",
            Application.Current?.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light);

    private sealed class State
    {
        public CancellationTokenSource? Cancellation;
        public PathIcon? Icon;
        public IBrush? Foreground;
        public bool Captured;
    }
}
