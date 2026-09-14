using System.Runtime.InteropServices;

namespace AegisVault.Platform;

/// <summary>
/// Keeps window content out of screenshots and screen recordings on Windows
/// via SetWindowDisplayAffinity (WDA_EXCLUDEFROMCAPTURE). Other platforms are
/// not supported.
/// </summary>
public static class ScreenCaptureGuard
{
    private const uint WdaNone = 0x00000000;
    private const uint WdaExcludeFromCapture = 0x00000011;

    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// Enables or disables capture exclusion for the given window handle.
    /// Returns false when unsupported or when the platform call fails.
    /// </summary>
    public static bool TrySetExcluded(nint hwnd, bool exclude)
    {
        if (!IsSupported || hwnd == 0)
        {
            return false;
        }

        try
        {
            return SetWindowDisplayAffinity(hwnd, exclude ? WdaExcludeFromCapture : WdaNone);
        }
        catch (Exception)
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);
}
