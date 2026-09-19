using AegisVault.Platform;
using Xunit;

namespace AegisVault.Platform.Tests;

public sealed class WindowActivationTests
{
    [Fact]
    public void SameExecutablePathIgnoresCaseAndWhitespace()
    {
        var path = Path.Combine(Path.GetTempPath(), "AegisVault.App.exe");

        Assert.True(WindowActivation.IsSameExecutablePath(path, "  " + path.ToUpperInvariant() + " "));
        Assert.False(WindowActivation.IsSameExecutablePath(path, Path.Combine(Path.GetTempPath(), "Other.exe")));
        Assert.False(WindowActivation.IsSameExecutablePath(null, path));
        Assert.False(WindowActivation.IsSameExecutablePath(path, null));
        Assert.False(WindowActivation.IsSameExecutablePath("   ", path));
    }

    [Fact]
    public void ActivationRejectsWindowsFromOtherExecutables()
    {
        // A window simply carrying the expected title must not be activated:
        // the owner process has to be this executable.
        var activated = WindowActivation.TryActivateByTitle(
            "AegisVault-No-Such-Window-Title",
            Environment.ProcessPath);

        Assert.False(activated);
    }
}
