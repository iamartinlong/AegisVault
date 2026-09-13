using System.Text;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class TotpServiceTests
{
    // RFC 6238 Appendix B test secrets (ASCII).
    private static readonly byte[] SecretSha1 = Encoding.ASCII.GetBytes("12345678901234567890");
    private static readonly byte[] SecretSha256 = Encoding.ASCII.GetBytes("12345678901234567890123456789012");
    private static readonly byte[] SecretSha512 = Encoding.ASCII.GetBytes(
        "1234567890123456789012345678901234567890123456789012345678901234");

    [Theory]
    [InlineData(59, "94287082")]
    [InlineData(1111111109, "07081804")]
    [InlineData(1111111111, "14050471")]
    [InlineData(1234567890, "89005924")]
    [InlineData(2000000000, "69279037")]
    [InlineData(20000000000, "65353130")]
    public void MatchesRfc6238Sha1Vectors(long unixTime, string expected)
    {
        var code = TotpService.GenerateCode(SecretSha1, DateTimeOffset.FromUnixTimeSeconds(unixTime), digits: 8);
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(59, "46119246")]
    [InlineData(1111111109, "68084774")]
    [InlineData(1111111111, "67062674")]
    [InlineData(1234567890, "91819424")]
    [InlineData(2000000000, "90698825")]
    [InlineData(20000000000, "77737706")]
    public void MatchesRfc6238Sha256Vectors(long unixTime, string expected)
    {
        var code = TotpService.GenerateCode(
            SecretSha256,
            DateTimeOffset.FromUnixTimeSeconds(unixTime),
            digits: 8,
            algorithm: TotpAlgorithm.Sha256);

        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(59, "90693936")]
    [InlineData(1111111109, "25091201")]
    [InlineData(1111111111, "99943326")]
    [InlineData(1234567890, "93441116")]
    [InlineData(2000000000, "38618901")]
    [InlineData(20000000000, "47863826")]
    public void MatchesRfc6238Sha512Vectors(long unixTime, string expected)
    {
        var code = TotpService.GenerateCode(
            SecretSha512,
            DateTimeOffset.FromUnixTimeSeconds(unixTime),
            digits: 8,
            algorithm: TotpAlgorithm.Sha512);

        Assert.Equal(expected, code);
    }

    [Fact]
    public void GeneratesSixDigitsByDefault()
    {
        var code = TotpService.GenerateCode(SecretSha1, DateTimeOffset.FromUnixTimeSeconds(59));

        Assert.Equal(6, code.Length);
        Assert.All(code, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void DecodeBase32KnownValue()
    {
        var decoded = TotpService.DecodeBase32("JBSWY3DPEHPK3PXP");

        var expected = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x21, 0xDE, 0xAD, 0xBE, 0xEF };
        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void Base32RoundTrip()
    {
        var data = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };

        var encoded = TotpService.EncodeBase32(data);
        var decoded = TotpService.DecodeBase32(encoded);

        Assert.Equal(data, decoded);
    }

    [Fact]
    public void DecodeBase32ToleratesSpacesLowercaseAndPadding()
    {
        var decoded = TotpService.DecodeBase32("jbsw y3dp ehpk3pxp====");

        Assert.Equal(10, decoded.Length);
    }

    [Fact]
    public void DecodeBase32RejectsInvalidCharacters()
    {
        Assert.Throws<FormatException>(() => TotpService.DecodeBase32("JBSWY3DP1HPK3PXP"));
    }

    [Fact]
    public void GenerateCodeFromBase32Secret()
    {
        // "JBSWY3DPEHPK3PXP" decodes to "Hello!" + 0xDEADBEEF (10 bytes).
        var code = TotpService.GenerateCode("JBSWY3DPEHPK3PXP", DateTimeOffset.FromUnixTimeSeconds(59));

        Assert.Equal(6, code.Length);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 29)]
    [InlineData(29, 1)]
    [InlineData(30, 30)]
    [InlineData(59, 1)]
    [InlineData(60, 30)]
    public void ComputesRemainingSeconds(long unixTime, int expected)
    {
        Assert.Equal(expected, TotpService.GetRemainingSeconds(DateTimeOffset.FromUnixTimeSeconds(unixTime)));
    }

    [Fact]
    public void RejectsEmptySecret()
    {
        Assert.Throws<ArgumentException>(() => TotpService.GenerateCode("", DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    public void RejectsInvalidDigitCount(int digits)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TotpService.GenerateCode(SecretSha1, DateTimeOffset.FromUnixTimeSeconds(59), digits));
    }
}
