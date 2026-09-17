using System.Runtime.InteropServices;
using System.Text;

namespace AegisVault.Platform;

/// <summary>
/// Brings an already running instance's window to the front (used by the
/// single-instance guard: a second launch activates the existing window instead
/// of starting a second process). Windows only; other platforms report false.
/// </summary>
public static class WindowActivation
{
    private const int SwRestore = 9;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const int HwndTop = 0;

    /// <summary>
    /// Finds a visible top-level window whose title matches
    /// <paramref name="title"/> and activates it. Returns false when no such
    /// window exists (e.g. the first instance is still starting up).
    /// </summary>
    public static bool TryActivateByTitle(string title)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        try
        {
            var handle = FindWindowByTitle(title);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            // The freshly launched process usually owns the foreground right,
            // which is what allows it to hand focus to the running instance.
            AllowSetForegroundWindow(-1 /* ASFW_ANY */);

            _ = ShowWindow(handle, SwRestore);
            _ = SetWindowPos(handle, HwndTop, 0, 0, 0, 0, SwpNoMove | SwpNoSize);
            _ = SetForegroundWindow(handle);
            return true;
        }
        catch (Exception)
        {
            // Activation is best effort; the second launch still exits cleanly.
            return false;
        }
    }

    /// <summary>Whether a window title should match the expected instance title.</summary>
    internal static bool IsTitleMatch(string? windowTitle, string expectedTitle)
        => !string.IsNullOrWhiteSpace(windowTitle) &&
           string.Equals(windowTitle.Trim(), expectedTitle.Trim(), StringComparison.Ordinal);

    private static IntPtr FindWindowByTitle(string title)
    {
        var found = IntPtr.Zero;
        var expected = title.Trim();

        _ = EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle))
            {
                return true;
            }

            var buffer = new StringBuilder(256);
            _ = GetWindowTextW(handle, buffer, buffer.Capacity);
            if (!IsTitleMatch(buffer.ToString(), expected))
            {
                return true;
            }

            // Prefer a real top-level window; skip message-only helpers.
            GetWindowThreadProcessId(handle, out var processId);
            if (processId == 0)
            {
                return true;
            }

            found = handle;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, int insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
