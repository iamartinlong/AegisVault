using System.Globalization;
using Avalonia;

namespace AegisVault.App.Services;

/// <summary>
/// Parses and clamps the persisted floating-ball placement. Kept free of window
/// types so it can be unit tested without a UI session.
/// </summary>
public static class BallPlacement
{
    public const string DockLeft = "left";
    public const string DockRight = "right";

    public static bool TryParse(string? value, out PixelPoint position)
    {
        position = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            return false;
        }

        position = new PixelPoint(x, y);
        return true;
    }

    public static string Format(PixelPoint position) => $"{position.X},{position.Y}";

    public static string? NormalizeSide(string? side)
        => side is DockLeft or DockRight ? side : null;

    /// <summary>Keeps the ball inside the given work area (taskbar aware).</summary>
    public static PixelPoint Clamp(PixelPoint position, PixelRect workArea, int width, int height)
        => new(
            Math.Clamp(position.X, workArea.X, Math.Max(workArea.X, workArea.Right - width)),
            Math.Clamp(position.Y, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - height)));
}
