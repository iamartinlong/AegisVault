namespace AegisVault.Core.Services;

/// <summary>Strength buckets, ordered from weakest to strongest.</summary>
public enum PasswordStrengthLabel
{
    VeryWeak,
    Weak,
    Fair,
    Strong,
    VeryStrong,
}

/// <summary>Actionable advice; the UI layer maps these to localized text.</summary>
[Flags]
public enum PasswordAdvice
{
    None = 0,
    Empty = 1,
    CommonPassword = 2,
    Repetitive = 4,
    TooShort = 8,
    MixedCase = 16,
    AddDigit = 32,
    AddSymbol = 64,
}

public sealed record PasswordStrengthResult(
    int Score,
    PasswordStrengthLabel Label,
    double EntropyBits,
    TimeSpan CrackTime,
    IReadOnlyList<PasswordAdvice> Suggestions);

/// <summary>
/// Pragmatic password strength estimation: entropy from length and character
/// classes, penalised for common passwords and repetitive patterns, with an
/// offline crack-time estimate (10^10 guesses/second). Language-neutral: the
/// result carries an enum label and advice codes, never user-facing text.
/// </summary>
public static class PasswordStrengthEstimator
{
    private const double GuessesPerSecond = 1e10;

    private static readonly string[] CommonPasswords =
    [
        "password", "123456", "123456789", "12345678", "qwerty", "111111", "abc123",
        "password1", "letmein", "welcome", "monkey", "dragon", "master", "admin",
        "iloveyou", "sunshine", "princess", "football", "baseball", "superman",
        "trustno1", "000000", "qwertyuiop", "1q2w3e4r", "zxcvbnm",
    ];

    public static PasswordStrengthResult Evaluate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordStrengthResult(
                0,
                PasswordStrengthLabel.VeryWeak,
                0,
                TimeSpan.Zero,
                [PasswordAdvice.Empty]);
        }

        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        var hasSymbol = password.Any(static c => !char.IsLetterOrDigit(c));

        var poolSize = 0;
        if (hasLower)
        {
            poolSize += 26;
        }

        if (hasUpper)
        {
            poolSize += 26;
        }

        if (hasDigit)
        {
            poolSize += 10;
        }

        if (hasSymbol)
        {
            poolSize += 33;
        }

        var entropy = password.Length * Math.Log2(Math.Max(poolSize, 2));

        var lower = password.ToLowerInvariant();
        var isCommon = CommonPasswords.Any(common => lower.Contains(common, StringComparison.Ordinal));
        var distinctCharacters = password.Distinct().Count();
        var isRepetitive = distinctCharacters <= Math.Max(2, password.Length / 4);

        var adjustment = 0.0;
        if (isCommon)
        {
            adjustment += entropy;
        }

        if (isRepetitive)
        {
            adjustment += entropy * 0.7;
        }

        var effectiveEntropy = Math.Max(0, entropy - adjustment);

        var suggestions = new List<PasswordAdvice>();
        if (isCommon)
        {
            suggestions.Add(PasswordAdvice.CommonPassword);
        }

        if (isRepetitive)
        {
            suggestions.Add(PasswordAdvice.Repetitive);
        }

        if (password.Length < 12)
        {
            suggestions.Add(PasswordAdvice.TooShort);
        }

        if (!(hasLower && hasUpper))
        {
            suggestions.Add(PasswordAdvice.MixedCase);
        }

        if (!hasDigit)
        {
            suggestions.Add(PasswordAdvice.AddDigit);
        }

        if (!hasSymbol)
        {
            suggestions.Add(PasswordAdvice.AddSymbol);
        }

        var score = effectiveEntropy switch
        {
            >= 100 => 4,
            >= 75 => 3,
            >= 50 => 2,
            >= 30 => 1,
            _ => 0,
        };

        var label = score switch
        {
            4 => PasswordStrengthLabel.VeryStrong,
            3 => PasswordStrengthLabel.Strong,
            2 => PasswordStrengthLabel.Fair,
            1 => PasswordStrengthLabel.Weak,
            _ => PasswordStrengthLabel.VeryWeak,
        };

        var crackSeconds = Math.Pow(2, Math.Min(effectiveEntropy, 128)) / GuessesPerSecond;
        var crackTime = crackSeconds >= TimeSpan.MaxValue.TotalSeconds
            ? TimeSpan.MaxValue
            : TimeSpan.FromSeconds(crackSeconds);

        return new PasswordStrengthResult(score, label, Math.Round(entropy, 1), crackTime, suggestions);
    }
}
