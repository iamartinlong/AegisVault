using System.Runtime.InteropServices;

namespace AegisVault.Platform;

/// <summary>
/// Registers a system-wide hotkey (default Ctrl+Shift+Space) on Windows using
/// a hidden message-only window on a dedicated background thread. Other
/// platforms are not supported and all calls become no-ops.
/// </summary>
public sealed class HotKeyService : IDisposable
{
    public const int ModAlt = 0x0001;
    public const int ModControl = 0x0002;
    public const int ModShift = 0x0004;
    public const int ModWin = 0x0008;

    private const uint WmHotKey = 0x0312;
    private const uint WmClose = 0x0010;
    private const uint WmQuit = 0x0012;
    private const uint HwndMessage = unchecked((uint)-3);

    private static HotKeyService? _active;
    private static readonly WndProcDelegate WndProcCallback = WndProc;

    private readonly uint _id = 1;
    private int _disposed;
    private Thread? _thread;
    private nint _hwnd;
    private uint _threadId;
    private volatile bool _registered;

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsRegistered => _registered;

    /// <summary>Raised on the hotkey message thread when the hotkey fires.</summary>
    public event Action? HotKeyPressed;

    public bool TryRegister(int modifiers, int virtualKey)
    {
        if (!IsSupported || _registered || _thread is not null || _disposed != 0)
        {
            return false;
        }

        _active = this;
        using var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() => RunMessageLoop((uint)modifiers, (uint)virtualKey, ready))
        {
            IsBackground = true,
            Name = "AegisVault.HotKey",
        };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
        return _registered;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (OperatingSystem.IsWindows() && _hwnd != 0)
        {
            UnregisterHotKey(_hwnd, _id);
            PostMessageW(_hwnd, WmClose, 0, 0);
            if (_threadId != 0)
            {
                PostThreadMessageW(_threadId, WmQuit, 0, 0);
            }
        }

        if (ReferenceEquals(_active, this))
        {
            _active = null;
        }
    }

    private void RunMessageLoop(uint modifiers, uint virtualKey, ManualResetEventSlim ready)
    {
        try
        {
            _threadId = GetCurrentThreadId();

            var wndClass = new WndClassW
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProcCallback),
                lpszClassName = "AegisVaultHotKeyWnd",
                hInstance = GetModuleHandleW(null),
            };
            var atom = RegisterClassW(ref wndClass);
            if (atom == 0)
            {
                ready.Set();
                return;
            }

            _hwnd = CreateWindowExW(
                0,
                "AegisVaultHotKeyWnd",
                string.Empty,
                0,
                0, 0, 0, 0,
                HwndMessage,
                0, 0, 0);
            if (_hwnd == 0)
            {
                ready.Set();
                return;
            }

            _registered = RegisterHotKey(_hwnd, _id, modifiers, virtualKey);
            ready.Set();
            if (!_registered)
            {
                return;
            }

            while (GetMessageW(out _, 0, 0, 0) > 0)
            {
            }
        }
        catch (Exception)
        {
            _registered = false;
        }
        finally
        {
            ready.Set();
        }
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmHotKey)
        {
            _active?.HotKeyPressed?.Invoke();
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassW
    {
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassW(ref WndClassW lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        uint hWndParent, uint hMenu, uint hInstance, uint lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, uint id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, uint id);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out NativeMessage lpMsg, uint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint threadId, uint msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public nint ptX;
        public nint ptY;
    }
}
