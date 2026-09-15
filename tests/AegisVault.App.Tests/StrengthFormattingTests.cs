using AegisVault.App.Services;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class StrengthFormattingTests
{
    [Theory]
    [InlineData(PasswordStrengthLabel.VeryWeak, "极弱")]
    [InlineData(PasswordStrengthLabel.Weak, "弱")]
    [InlineData(PasswordStrengthLabel.Fair, "中等")]
    [InlineData(PasswordStrengthLabel.Strong, "强")]
    [InlineData(PasswordStrengthLabel.VeryStrong, "极强")]
    public void FormatsLabels(PasswordStrengthLabel label, string expected)
        => Assert.Equal(expected, StrengthFormatting.FormatLabel(label));

    [Fact]
    public void FormatsSummary()
        => Assert.Equal(
            "极强 · 离线破解约 超过 1000 年",
            StrengthFormatting.FormatSummary(4, PasswordStrengthLabel.VeryStrong, TimeSpan.MaxValue));

    [Fact]
    public void EmptySummaryForEmptyPassword()
        => Assert.Equal(string.Empty, StrengthFormatting.FormatSummary(0, PasswordStrengthLabel.VeryWeak, TimeSpan.Zero));

    [Fact]
    public void FormatsInstantCrackTime()
        => Assert.Equal("立即", StrengthFormatting.FormatCrackTime(TimeSpan.Zero));

    [Fact]
    public void FormatsAdvice()
        => Assert.Equal("该密码出现在常见密码列表中", StrengthFormatting.FormatAdvice(PasswordAdvice.CommonPassword));
}
