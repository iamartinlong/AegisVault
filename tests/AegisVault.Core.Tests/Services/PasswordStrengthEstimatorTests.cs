using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class PasswordStrengthEstimatorTests
{
    [Fact]
    public void CommonPasswordScoresZero()
    {
        var result = PasswordStrengthEstimator.Evaluate("password123");

        Assert.Equal(0, result.Score);
        Assert.Contains(PasswordAdvice.CommonPassword, result.Suggestions);
    }

    [Fact]
    public void RepetitivePatternIsPenalised()
    {
        var result = PasswordStrengthEstimator.Evaluate("aaaaaaaaaaaaaaaa");

        Assert.True(result.Score <= 1, $"Expected <= 1, got {result.Score}.");
        Assert.Contains(PasswordAdvice.Repetitive, result.Suggestions);
    }

    [Fact]
    public void ShortPasswordIsWeak()
    {
        var result = PasswordStrengthEstimator.Evaluate("aB3!");

        Assert.True(result.Score <= 1);
        Assert.Contains(PasswordAdvice.TooShort, result.Suggestions);
    }

    [Fact]
    public void GeneratedPasswordIsStrong()
    {
        var password = PasswordGenerator.Generate(new());
        var result = PasswordStrengthEstimator.Evaluate(password);

        Assert.True(result.Score >= 3, $"Expected >= 3, got {result.Score} for '{password}'.");
    }

    [Fact]
    public void LongPassphraseIsStrong()
    {
        var result = PasswordStrengthEstimator.Evaluate("correct-horse-battery-staple-42");

        Assert.True(result.Score >= 3);
    }

    [Fact]
    public void StrongerPasswordHasLongerCrackTime()
    {
        var weak = PasswordStrengthEstimator.Evaluate("abcd1234");
        var strong = PasswordStrengthEstimator.Evaluate("kR7#mQ2!vX9@Lp4$");

        Assert.True(strong.CrackTime > weak.CrackTime);
    }

    [Fact]
    public void EmptyPasswordIsZero()
    {
        var result = PasswordStrengthEstimator.Evaluate(null);

        Assert.Equal(0, result.Score);
        Assert.Equal(PasswordStrengthLabel.VeryWeak, result.Label);
        Assert.Contains(PasswordAdvice.Empty, result.Suggestions);
    }

    [Fact]
    public void MissingClassesProduceSuggestions()
    {
        var result = PasswordStrengthEstimator.Evaluate("abcdefghijklmnop");

        Assert.Contains(PasswordAdvice.MixedCase, result.Suggestions);
        Assert.Contains(PasswordAdvice.AddDigit, result.Suggestions);
        Assert.Contains(PasswordAdvice.AddSymbol, result.Suggestions);
    }

    [Fact]
    public void LabelsMatchScoreBuckets()
    {
        Assert.Equal(PasswordStrengthLabel.VeryWeak, PasswordStrengthEstimator.Evaluate("abc").Label);
        Assert.Equal(PasswordStrengthLabel.VeryStrong, PasswordStrengthEstimator.Evaluate("kR7#mQ2!vX9@Lp4$wZt6&").Label);
    }
}
