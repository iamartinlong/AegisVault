using System.Security.Cryptography;
using System.Text;
using AegisVault.Core.Crypto;
using Xunit;

namespace AegisVault.Core.Tests.Crypto;

public sealed class KeyEnvelopeTests
{
    private static byte[] NewKek() => RandomNumberGenerator.GetBytes(32);

    private static byte[] NewDek() => RandomNumberGenerator.GetBytes(KeyEnvelope.DekSize);

    [Fact]
    public void WrapUnwrapRoundTrip()
    {
        var kek = NewKek();
        var dek = NewDek();
        var aad = Encoding.UTF8.GetBytes("vault:1");

        var blob = KeyEnvelope.Wrap(kek, dek, aad);
        using var unwrapped = KeyEnvelope.Unwrap(kek, blob, aad);

        Assert.Equal(dek, unwrapped.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void BlobHasExpectedSize()
    {
        var blob = KeyEnvelope.Wrap(NewKek(), NewDek(), default);
        Assert.Equal(KeyEnvelope.BlobSize, blob.Length);
    }

    [Fact]
    public void WrongKekIsRejected()
    {
        var aad = Encoding.UTF8.GetBytes("vault:1");
        var blob = KeyEnvelope.Wrap(NewKek(), NewDek(), aad);

        Assert.ThrowsAny<CryptographicException>(() => KeyEnvelope.Unwrap(NewKek(), blob, aad));
    }

    [Fact]
    public void WrongAssociatedDataIsRejected()
    {
        var kek = NewKek();
        var blob = KeyEnvelope.Wrap(kek, NewDek(), Encoding.UTF8.GetBytes("vault:1"));

        Assert.ThrowsAny<CryptographicException>(() => KeyEnvelope.Unwrap(kek, blob, Encoding.UTF8.GetBytes("vault:2")));
    }

    [Fact]
    public void TamperedBlobIsRejected()
    {
        var kek = NewKek();
        var aad = Encoding.UTF8.GetBytes("vault:1");
        var blob = KeyEnvelope.Wrap(kek, NewDek(), aad);

        blob[AesGcmCipher.NonceSize + 1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => KeyEnvelope.Unwrap(kek, blob, aad));
    }

    [Fact]
    public void RejectsInvalidDekSize()
    {
        Assert.Throws<ArgumentException>(() => KeyEnvelope.Wrap(NewKek(), new byte[16], default));
    }
}
