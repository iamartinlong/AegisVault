using System.Threading;

namespace AegisVault.Core.Crypto;

/// <summary>
/// A fixed-size byte buffer allocated through libsodium's secure allocator
/// (<c>sodium_malloc</c>): guarded pages, best-effort locking against swap,
/// and guaranteed zeroing on dispose.
/// </summary>
public sealed unsafe class SecureBuffer : IDisposable
{
    private static readonly Lock InitLock = new();
    private static bool _initialized;

    private IntPtr _pointer;
    private readonly int _length;
    private bool _readOnly;
    private bool _disposed;

    public SecureBuffer(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        EnsureInitialized();

        _pointer = LibsodiumNative.Malloc((nuint)length);
        if (_pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Secure memory allocation failed.");
        }

        _length = length;
        LibsodiumNative.MLock(_pointer, (nuint)length);
    }

    ~SecureBuffer() => Dispose(disposing: false);

    public int Length => _length;

    public bool IsReadOnly => _readOnly;

    /// <summary>Writable view. Throws when the buffer is protected read-only.</summary>
    public Span<byte> Span
    {
        get
        {
            ThrowIfDisposed();
            if (_readOnly)
            {
                throw new InvalidOperationException("The secure buffer is read-only.");
            }

            return new Span<byte>(_pointer.ToPointer(), _length);
        }
    }

    /// <summary>Read-only view, valid also while the buffer is protected.</summary>
    public ReadOnlySpan<byte> ReadOnlySpan
    {
        get
        {
            ThrowIfDisposed();
            return new ReadOnlySpan<byte>(_pointer.ToPointer(), _length);
        }
    }

    public static SecureBuffer From(ReadOnlySpan<byte> data)
    {
        var buffer = new SecureBuffer(data.Length);
        data.CopyTo(buffer.Span);
        return buffer;
    }

    public void ProtectReadOnly()
    {
        ThrowIfDisposed();
        if (!_readOnly)
        {
            var result = LibsodiumNative.MProtectReadOnly(_pointer, (nuint)_length);
            if (result != 0)
            {
                throw new InvalidOperationException($"sodium_mprotect_readonly failed ({result}).");
            }

            _readOnly = true;
        }
    }

    public void ProtectReadWrite()
    {
        ThrowIfDisposed();
        if (_readOnly)
        {
            var result = LibsodiumNative.MProtectReadWrite(_pointer, (nuint)_length);
            if (result != 0)
            {
                throw new InvalidOperationException($"sodium_mprotect_readwrite failed ({result}).");
            }

            _readOnly = false;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var pointer = Interlocked.Exchange(ref _pointer, IntPtr.Zero);
        if (pointer == IntPtr.Zero)
        {
            return;
        }

        if (_readOnly)
        {
            LibsodiumNative.MProtectReadWrite(pointer, (nuint)_length);
            _readOnly = false;
        }

        LibsodiumNative.MemZero(pointer, (nuint)_length);
        LibsodiumNative.MUnlock(pointer, (nuint)_length);
        LibsodiumNative.Free(pointer);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            // sodium_init returns 0 on first initialization, 1 when already
            // initialized and -1 on failure.
            var result = LibsodiumNative.Init();
            if (result < 0)
            {
                throw new InvalidOperationException($"sodium_init failed ({result}).");
            }

            _initialized = true;
        }
    }
}
