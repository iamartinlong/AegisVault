using Microsoft.Win32;
using AegisVault.Platform;
using Xunit;

namespace AegisVault.Platform.Tests;

public sealed class WindowsStartupRegistrationTests
{
    [Fact]
    public void EnableDisableRoundTripsWithACaseInsensitivePathCheck()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Disposable HKCU sub-key; the real Run key is never touched by tests.
        var keyPath = $@"Software\AegisVault\Tests\{Guid.NewGuid():N}";
        var registration = new WindowsStartupRegistration(keyPath);

        try
        {
            Assert.True(registration.IsSupported);
            Assert.False(registration.IsEnabled(@"C:\apps\aegis.exe"));

            Assert.True(registration.TrySetEnabled(true, @"C:\apps\aegis.exe", "--tray"));
            Assert.True(registration.IsEnabled(@"C:\apps\aegis.exe"));
            Assert.True(registration.IsEnabled(@"c:\APPS\Aegis.exe"));
            Assert.False(registration.IsEnabled(@"C:\other\aegis.exe"));

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false))
            {
                var command = Assert.IsType<string>(key?.GetValue(WindowsStartupRegistration.ValueName));
                Assert.Contains("--tray", command);
                Assert.StartsWith("\"C:\\apps\\aegis.exe\"", command);
            }

            Assert.True(registration.TrySetEnabled(false, @"C:\apps\aegis.exe"));
            Assert.False(registration.IsEnabled(@"C:\apps\aegis.exe"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void BlankExecutableIsRejected()
    {
        var registration = new WindowsStartupRegistration();

        Assert.False(registration.IsEnabled("  "));
        Assert.False(registration.TrySetEnabled(true, "   "));
    }
}
