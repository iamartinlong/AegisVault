using Avalonia;

namespace AegisVault.App.Services;

/// <summary>
/// Persisted main-window placement ("x,y,w,h") kept in the plaintext preferences.
/// Only geometry lives here: the rectangle is validated against the current
/// screens so a layout change (undocked laptop, monitor removed) cannot restore
/// the window off-screen.
/// </summary>
public static class WindowPlacement
{
    public static string Format(PixelPoint position, PixelSize size)
        => $"{position.X},{position.Y},{size.Width},{size.Height}";

    public static bool TryParse(string? text, out PixelPoint position, out PixelSize size)
    {
        position = default;
        size = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 ||
            !int.TryParse(parts[0], out var x) ||
            !int.TryParse(parts[1], out var y) ||
            !int.TryParse(parts[2], out var width) ||
            !int.TryParse(parts[3], out var height))
        {
            return false;
        }

        // A zero/negative size would make the window unusable.
        if (width < 320 || height < 240)
        {
            return false;
        }

        position = new PixelPoint(x, y);
        size = new PixelSize(width, height);
        return true;
    }

    /// <summary>True when the rectangle still overlaps at least one screen.</summary>
    public static bool IsVisibleOnScreens(PixelPoint position, PixelSize size, IEnumerable<Rect> screens)
    {
        var rect = new Rect(position.X, position.Y, size.Width, size.Height);

        // Require a usable overlap, not just a touching edge: a window that is 99%
        // off-screen is as good as lost.
        var minimumWidth = Math.Min(120d, size.Width);
        var minimumHeight = Math.Min(60d, size.Height);
        foreach (var screen in screens)
        {
            var overlap = screen.Intersect(rect);
            if (overlap.Width >= minimumWidth && overlap.Height >= minimumHeight)
            {
                return true;
            }
        }

        return false;
    }
}
