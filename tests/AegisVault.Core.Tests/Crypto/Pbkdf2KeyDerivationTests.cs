using System.Security.Cryptography;
using System.Text;
using AegisVault.Core.Crypto;
using AegisVault.Core.Models;
using Xunit;

namespace AegisVault.Core.Tests.Crypto;

public sealed class Pbkdf2KeyDerivationTests
{
    private static byte[] Derive(string password, string salt, int iterations, int keySize)
    {
        var derivation = new Pbkdf2KeyDerivation();
        var parameters = new KdfParameters
        {
            Algorithm = KdfParameters.AlgorithmPbkdf2Sha256,
            Iterations = iterations,
            KeySize = keySize,
        };

        using var key = derivation.DeriveKey(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(salt), parameters);
        return key.ReadOnlySpan.ToArray();
    }

    // Well-known PBKDF2-HMAC-SHA256 vectors (cross-checked with hashlib.pbkdf2_hmac).
    [Theory]
    [InlineData(1, "120fb6cffcf8b32c43e7225256c4f837a86548c92ccc35480805987cb70be17b")]
    [InlineData(2, "ae4d0c95af6b46d32d0adff928f06dd02a303f8ef3c251dfd6e2d85a95474c43")]
    [InlineData(4096, "c5e478d59288c841aa530db6845c4c8d962893a001ce4e11a4963873aa98134a")]
    public void MatchesKnownVectors(int iterations, string expectedHex)
    {
        var actual = Derive("password", "salt", iterations, 32);

        Assert.Equal(expectedHex, Convert.ToHexString(actual).ToLowerInvariant());
    }

    [Fact]
    public void MatchesKnownVectorWithLongPasswordAndSalt()
    {
        var actual = Derive(
            "passwordPASSWORDpassword",
            "saltSALTsaltSALTsaltSALTsaltSALTsalt",
            4096,
            40);

        Assert.Equal(
            "348c89dbcbd32b2f32d814b8116e84cf2b17347ebc1800181c4e2a1fb8dd53e1c635518c7dac47e9",
            Convert.ToHexString(actual).ToLowerInvariant());
    }

    [Fact]
    public void IsDeterministic()
    {
        var first = Derive("master", "salt-1234567890ab", 1000, 32);
        var second = Derive("master", "salt-1234567890ab", 1000, 32);

        Assert.Equal(first, second);
    }

    [Fact]
    public void RejectsEmptySalt()
    {
        var derivation = new Pbkdf2KeyDerivation();
        var parameters = new KdfParameters { Algorithm = KdfParameters.AlgorithmPbkdf2Sha256 };

        Assert.Throws<ArgumentException>(() =>
            derivation.DeriveKey(Encoding.UTF8.GetBytes("x"), ReadOnlySpan<byte>.Empty, parameters));
    }

    [Fact]
    public void RejectsMismatchedAlgorithm()
    {
        var derivation = new Pbkdf2KeyDerivation();
        var parameters = new KdfParameters { Algorithm = KdfParameters.AlgorithmArgon2id };

        Assert.Throws<ArgumentException>(() =>
            derivation.DeriveKey(Encoding.UTF8.GetBytes("x"), RandomNumberGenerator.GetBytes(16), parameters));
    }
}
