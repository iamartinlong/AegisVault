using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class RecentVaultsTests
{
    [Fact]
    public void NormalizeDropsBlanksAndTrims()
        => Assert.Equal([@"C:\a\v.aegis"], RecentVaults.Normalize(["  C:\\a\\v.aegis ", "", "   ", null!]));

    [Fact]
    public void NormalizeIsCaseInsensitiveAndKeepsFirstOccurrence()
        => Assert.Equal([@"C:\a\v.aegis"], RecentVaults.Normalize([@"C:\a\v.aegis", @"c:\A\V.aegis"]));

    [Fact]
    public void NormalizeCapsAtThree()
        => Assert.Equal(
            [@"C:\1.aegis", @"C:\2.aegis", @"C:\3.aegis"],
            RecentVaults.Normalize([@"C:\1.aegis", @"C:\2.aegis", @"C:\3.aegis", @"C:\4.aegis"]));

    [Fact]
    public void NormalizeOfNullIsEmpty()
        => Assert.Empty(RecentVaults.Normalize(null));

    [Fact]
    public void AddMovesThePathToTheFront()
        => Assert.Equal(
            [@"C:\b.aegis", @"C:\a.aegis"],
            RecentVaults.Add([@"C:\a.aegis", @"C:\b.aegis"], @"C:\b.aegis"));

    [Fact]
    public void AddCapsAndDropsTheOldest()
        => Assert.Equal(
            [@"C:\d.aegis", @"C:\a.aegis", @"C:\b.aegis"],
            RecentVaults.Add([@"C:\a.aegis", @"C:\b.aegis", @"C:\c.aegis"], @"C:\d.aegis"));

    [Fact]
    public void AddIgnoresBlankPaths()
        => Assert.Equal([@"C:\a.aegis"], RecentVaults.Add([@"C:\a.aegis"], "  "));

    [Fact]
    public void AddTrimsTheNewPath()
        => Assert.Equal([@"C:\a.aegis"], RecentVaults.Add(null, "  C:\\a.aegis  "));
}
