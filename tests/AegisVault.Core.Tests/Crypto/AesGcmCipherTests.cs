using System.Security.Cryptography;
using System.Text;
using AegisVault.Core.Crypto;
using Xunit;

namespace AegisVault.Core.Tests.Crypto;

public sealed class AesGcmCipherTests
{
    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(AesGcmCipher.KeySize);

    [Fact]
    public void RoundTripReturnsOriginalPlaintext()
    {
        var key = NewKey();
        var plaintext = Encoding.UTF8.GetBytes("{\"title\":\"GitHub\",\"password\":\"s3cret\"}");
        var aad = Encoding.UTF8.GetBytes("entry:1:version:1");

        var (nonce, ciphertext, tag) = AesGcmCipher.Encrypt(key, plaintext, aad);
        var decrypted = AesGcmCipher.Decrypt(key, nonce, ciphertext, tag, aad);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void NonceIsFreshForEveryEncryption()
    {
        var key = NewKey();
        var plaintext = Encoding.UTF8.GetBytes("same plaintext");
        byte[] aad = [];

        var first = AesGcmCipher.Encrypt(key, plaintext, aad);
        var second = AesGcmCipher.Encrypt(key, plaintext, aad);

        Assert.NotEqual(first.Nonce, second.Nonce);
    }

    [Fact]
    public void TamperedCiphertextIsRejected()
    {
        var key = NewKey();
        var plaintext = Encoding.UTF8.GetBytes("payload");
        byte[] aad = [];

        var (nonce, ciphertext, tag) = AesGcmCipher.Encrypt(key, plaintext, aad);
        ciphertext[0] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, nonce, ciphertext, tag, aad));
    }

    [Fact]
    public void TamperedTagIsRejected()
    {
        var key = NewKey();
        var plaintext = Encoding.UTF8.GetBytes("payload");
        byte[] aad = [];

        var (nonce, ciphertext, tag) = AesGcmCipher.Encrypt(key, plaintext, aad);
        tag[0] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, nonce, ciphertext, tag, aad));
    }

    [Fact]
    public void TamperedAssociatedDataIsRejected()
    {
        var key = NewKey();
        var plaintext = Encoding.UTF8.GetBytes("payload");
        var aad = Encoding.UTF8.GetBytes("entry:1");

        var (nonce, ciphertext, tag) = AesGcmCipher.Encrypt(key, plaintext, aad);
        var wrongAad = Encoding.UTF8.GetBytes("entry:2");

        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, nonce, ciphertext, tag, wrongAad));
    }

    [Fact]
    public void WrongKeyIsRejected()
    {
        var key = NewKey();
        var plaintext = Encoding.UTF8.GetBytes("payload");
        byte[] aad = [];

        var (nonce, ciphertext, tag) = AesGcmCipher.Encrypt(key, plaintext, aad);
        var wrongKey = NewKey();

        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(wrongKey, nonce, ciphertext, tag, aad));
    }

    [Fact]
    public void RejectsInvalidKeySize()
    {
        var key = new byte[16];
        Assert.Throws<ArgumentException>(() => AesGcmCipher.Encrypt(key, new byte[1], default));
    }
}
