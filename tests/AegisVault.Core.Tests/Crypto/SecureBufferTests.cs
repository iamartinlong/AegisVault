using AegisVault.Core.Crypto;
using Xunit;

namespace AegisVault.Core.Tests.Crypto;

public sealed class SecureBufferTests
{
    [Fact]
    public void AllocatesAndRoundTripsData()
    {
        using var buffer = new SecureBuffer(32);

        Assert.Equal(32, buffer.Length);

        var span = buffer.Span;
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = (byte)i;
        }

        Assert.Equal((byte)5, buffer.ReadOnlySpan[5]);
    }

    [Fact]
    public void FromCopiesData()
    {
        var source = new byte[] { 1, 2, 3, 4 };

        using var buffer = SecureBuffer.From(source);

        Assert.Equal(source, buffer.ReadOnlySpan.ToArray());

        source[0] = 99;
        Assert.Equal(1, buffer.ReadOnlySpan[0]);
    }

    [Fact]
    public void ProtectReadOnlyBlocksWrites()
    {
        using var buffer = new SecureBuffer(16);

        buffer.ProtectReadOnly();
        Assert.True(buffer.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => _ = buffer.Span);
        Assert.Equal(16, buffer.ReadOnlySpan.Length);

        buffer.ProtectReadWrite();
        buffer.Span[0] = 1;
    }

    [Fact]
    public void DisposeIsIdempotentAndThrowsAfterwards()
    {
        var buffer = SecureBuffer.From(new byte[] { 9, 9, 9, 9 });

        buffer.Dispose();
        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = buffer.ReadOnlySpan);
    }

    [Fact]
    public void RejectsInvalidLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SecureBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SecureBuffer(-1));
    }
}
