using System.Runtime.InteropServices;

namespace AegisVault.Platform;

/// <summary>
/// Marks the current clipboard content as sensitive on Windows so the
/// clipboard history (Win+V), the cloud clipboard and clipboard-monitor
/// processing skip it. Must be called while the copying process still owns
/// the clipboard. Best effort: unsupported platforms and failures return false.
/// </summary>
public static class ClipboardExclusion
{
    private const uint GmemMoveable = 0x0002;
    private const uint GmemZeroInit = 0x0040;

    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// Adds the exclusion marker formats to the clipboard. <paramref name="hwnd"/>
    /// is the window that takes clipboard ownership for the call.
    /// </summary>
    public static bool TryMarkCurrent(nint hwnd)
    {
        if (!IsSupported || hwnd == 0)
        {
            return false;
        }

        try
        {
            if (!OpenClipboard(hwnd))
            {
                return false;
            }

            try
            {
                var history = TrySetDword("CanIncludeInClipboardHistory", 0);
                var cloud = TrySetDword("CanUploadToCloudClipboard", 0);
                var monitor = TrySetDword("ExcludeClipboardContentFromMonitorProcessing", 1);
                return history && cloud && monitor;
            }
            finally
            {
                CloseClipboard();
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TrySetDword(string formatName, int value)
    {
        var format = RegisterClipboardFormat(formatName);
        if (format == 0)
        {
            return false;
        }

        var memory = GlobalAlloc(GmemMoveable | GmemZeroInit, 4);
        if (memory == 0)
        {
            return false;
        }

        var pointer = GlobalLock(memory);
        if (pointer == 0)
        {
            GlobalFree(memory);
            return false;
        }

        Marshal.WriteInt32(pointer, value);
        GlobalUnlock(memory);

        if (SetClipboardData(format, memory) == 0)
        {
            GlobalFree(memory);
            return false;
        }

        // Ownership moved to the clipboard, which frees the block.
        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalFree(nint hMem);
}
