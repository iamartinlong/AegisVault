using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class VaultHealthTests
{
    [Fact]
    public void DetectsWeakAndReusedPasswords()
    {
        var now = DateTimeOffset.UtcNow;
        var report = VaultHealth.Analyze(
        [
            new PasswordEntry { Title = "A", Password = "correct horse battery staple 42!", UpdatedAt = now },
            new PasswordEntry { Title = "B", Password = "123456", UpdatedAt = now },
            new PasswordEntry { Title = "C", Password = "123456", UpdatedAt = now },
            new PasswordEntry { Title = "D", Password = "", UpdatedAt = now },
        ]);

        Assert.Equal(4, report.TotalEntries);
        Assert.Equal(2, report.WeakCount);
        Assert.Equal(2, report.ReusedCount);
        Assert.Equal(2, report.IssueCount);
        Assert.True(report.HasIssues);
    }

    [Fact]
    public void CleanVaultScoresFull()
    {
        var now = DateTimeOffset.UtcNow;
        var report = VaultHealth.Analyze(
        [
            new PasswordEntry { Title = "A", Password = "yJ9#mQ2vLx!pR7@wZt4&", UpdatedAt = now },
            new PasswordEntry { Title = "B", Password = "uT6$kWn3^bF8*eHc1+", UpdatedAt = now },
        ]);

        Assert.False(report.HasIssues);
        Assert.Equal(100, report.HealthScore);
        Assert.Equal("安全：2 条密码全部良好", $"安全：{report.TotalEntries} 条密码全部良好");
    }

    [Fact]
    public void CountsStaleEntries()
    {
        var report = VaultHealth.Analyze(
        [
            new PasswordEntry { Title = "A", Password = "x", UpdatedAt = DateTimeOffset.UtcNow - TimeSpan.FromDays(400) },
            new PasswordEntry { Title = "B", Password = "y", UpdatedAt = DateTimeOffset.UtcNow },
        ]);

        Assert.Equal(1, report.OldCount);
    }

    [Fact]
    public void EmptyVaultScoresFull()
    {
        var report = VaultHealth.Analyze(Array.Empty<PasswordEntry>());
        Assert.Equal(0, report.TotalEntries);
        Assert.Equal(100, report.HealthScore);
    }
}
