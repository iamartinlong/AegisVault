using System.Runtime.InteropServices;

namespace AegisVault.Platform;

/// <summary>
/// Best-effort process hardening: prevents the OS from writing core dumps
/// that could contain decrypted key material.
/// </summary>
public static partial class CoreDumpGuard
{
    private const uint SemFailCriticalErrors = 0x0001;
    private const uint SemNoGpFaultErrorBox = 0x0002;
    private const uint SemNoOpenFileErrorBox = 0x8000;
    private const int RlimitCore = 4;

    public static void DisableCoreDumps()
    {
        if (OperatingSystem.IsWindows())
        {
            SetErrorMode(SemFailCriticalErrors | SemNoGpFaultErrorBox | SemNoOpenFileErrorBox);
            return;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var limit = new RLimit { Current = 0, Maximum = 0 };
            setrlimit(RlimitCore, ref limit);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "SetErrorMode")]
    private static partial uint SetErrorMode(uint mode);

    [LibraryImport("libc", EntryPoint = "setrlimit")]
    private static partial int setrlimit(int resource, ref RLimit limit);

    [StructLayout(LayoutKind.Sequential)]
    private struct RLimit
    {
        public ulong Current;
        public ulong Maximum;
    }
}
