using System.Security.Cryptography;
using AegisVault.Platform;
using Xunit;

namespace AegisVault.Platform.Tests;

public sealed class DpapiKeyProtectorTests
{
    [Fact]
    public void RoundTripOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var protector = new DpapiKeyProtector();
        Assert.True(protector.IsAvailable);

        var data = RandomNumberGenerator.GetBytes(65);
        var blob = protector.Protect(data);

        Assert.NotEqual(data, blob);
        Assert.Equal(data, protector.Unprotect(blob));
    }

    [Fact]
    public void TamperedBlobIsRejected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var protector = new DpapiKeyProtector();
        var blob = protector.Protect(RandomNumberGenerator.GetBytes(65));
        blob[0] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(blob));
    }

    [Fact]
    public void CoreDumpGuardDoesNotThrow()
    {
        CoreDumpGuard.DisableCoreDumps();
    }

    [Fact]
    public void SecureInputAvailabilityMatchesOs()
    {
        Assert.Equal(OperatingSystem.IsWindows(), WindowsSecureInput.IsAvailable);
    }
}
