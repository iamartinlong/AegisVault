using System.Runtime.InteropServices;

namespace AegisVault.Core.Crypto;

/// <summary>
/// Minimal libsodium interop surface needed for secure memory handling.
/// Uses source-generated P/Invoke (NativeAOT friendly).
/// </summary>
internal static partial class LibsodiumNative
{
    private const string Library = "libsodium";

    [LibraryImport(Library, EntryPoint = "sodium_init")]
    internal static partial int Init();

    [LibraryImport(Library, EntryPoint = "sodium_malloc")]
    internal static partial IntPtr Malloc(nuint size);

    [LibraryImport(Library, EntryPoint = "sodium_free")]
    internal static partial void Free(IntPtr ptr);

    [LibraryImport(Library, EntryPoint = "sodium_memzero")]
    internal static partial void MemZero(IntPtr ptr, nuint length);

    [LibraryImport(Library, EntryPoint = "sodium_mlock")]
    internal static partial int MLock(IntPtr ptr, nuint length);

    [LibraryImport(Library, EntryPoint = "sodium_munlock")]
    internal static partial int MUnlock(IntPtr ptr, nuint length);

    [LibraryImport(Library, EntryPoint = "sodium_mprotect_readonly")]
    internal static partial int MProtectReadOnly(IntPtr ptr, nuint length);

    [LibraryImport(Library, EntryPoint = "sodium_mprotect_readwrite")]
    internal static partial int MProtectReadWrite(IntPtr ptr, nuint length);
}
