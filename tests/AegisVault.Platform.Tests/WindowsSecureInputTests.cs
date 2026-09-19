using System.Runtime.InteropServices;
using AegisVault.Platform;
using Xunit;

namespace AegisVault.Platform.Tests;

public sealed class WindowsSecureInputTests
{
    [Fact]
    public void ZeroBufferClearsTheContents()
    {
        const int size = 16;
        var buffer = Marshal.AllocCoTaskMem(size);
        try
        {
            Marshal.Copy(Enumerable.Repeat((byte)0x41, size).ToArray(), 0, buffer, size);

            WindowsSecureInput.ZeroBuffer(buffer, size);

            var bytes = new byte[size];
            Marshal.Copy(buffer, bytes, 0, size);
            Assert.All(bytes, value => Assert.Equal(0, value));
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }

    [Fact]
    public void ZeroBufferHandlesEmptyInput()
    {
        WindowsSecureInput.ZeroBuffer(IntPtr.Zero, 0);
    }
}
