using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class PasswordGeneratorTests
{
    [Fact]
    public void GeneratesRequestedLength()
    {
        var password = PasswordGenerator.Generate(new PasswordGeneratorOptions { Length = 37 });

        Assert.Equal(37, password.Length);
    }

    [Fact]
    public void RequiresEverySelectedClassByDefault()
    {
        var password = PasswordGenerator.Generate(new PasswordGeneratorOptions { Length = 16 });

        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, static c => "!@#$%^&*()-_=+[]{};:,.<>?".Contains(c));
    }

    [Fact]
    public void ExcludesAmbiguousCharacters()
    {
        const string ambiguous = "Il1O0o";

        for (var i = 0; i < 50; i++)
        {
            var password = PasswordGenerator.Generate(new PasswordGeneratorOptions
            {
                Length = 32,
                ExcludeAmbiguous = true,
            });

            Assert.DoesNotContain(password, c => ambiguous.Contains(c));
        }
    }

    [Fact]
    public void RespectsDisabledClasses()
    {
        var password = PasswordGenerator.Generate(new PasswordGeneratorOptions
        {
            Length = 40,
            IncludeSymbols = false,
            IncludeUppercase = false,
        });

        Assert.DoesNotContain(password, char.IsUpper);
        Assert.DoesNotContain(password, static c => "!@#$%^&*()-_=+[]{};:,.<>?".Contains(c));
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
    }

    [Fact]
    public void RejectsEmptyClassSelection()
    {
        var options = new PasswordGeneratorOptions
        {
            IncludeLowercase = false,
            IncludeUppercase = false,
            IncludeDigits = false,
            IncludeSymbols = false,
        };

        Assert.Throws<ArgumentException>(() => PasswordGenerator.Generate(options));
    }

    [Fact]
    public void RejectsTooShortLengthForRequiredClasses()
    {
        var options = new PasswordGeneratorOptions { Length = 2 };

        Assert.Throws<ArgumentException>(() => PasswordGenerator.Generate(options));
    }

    [Fact]
    public void ProducesDifferentPasswords()
    {
        var options = new PasswordGeneratorOptions { Length = 24 };

        Assert.NotEqual(PasswordGenerator.Generate(options), PasswordGenerator.Generate(options));
    }

    [Fact]
    public void EstimatesEntropy()
    {
        var options = new PasswordGeneratorOptions { Length = 20 };

        var bits = PasswordGenerator.EstimateEntropyBits(options);

        // 20 chars over a large pool must comfortably exceed 100 bits.
        Assert.True(bits > 100, $"Expected > 100 bits, got {bits:F1}.");
    }
}
