namespace AegisVault.Core.Services;

public sealed record PasswordStrengthResult(
    int Score,
    string Label,
    double EntropyBits,
    TimeSpan CrackTime,
    IReadOnlyList<string> Suggestions);

/// <summary>
/// Pragmatic password strength estimation: entropy from length and character
/// classes, penalised for common passwords and repetitive patterns, with an
/// offline crack-time estimate (10^10 guesses/second).
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
            return new PasswordStrengthResult(0, "极弱", 0, TimeSpan.Zero, ["请输入密码。"]);
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

        var suggestions = new List<string>();
        if (isCommon)
        {
            suggestions.Add("该密码出现在常见密码列表中");
        }

        if (isRepetitive)
        {
            suggestions.Add("避免重复或规律字符");
        }

        if (password.Length < 12)
        {
            suggestions.Add("长度建议 12 位以上");
        }

        if (!(hasLower && hasUpper))
        {
            suggestions.Add("混合大小写字母");
        }

        if (!hasDigit)
        {
            suggestions.Add("加入数字");
        }

        if (!hasSymbol)
        {
            suggestions.Add("加入符号");
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
            4 => "极强",
            3 => "强",
            2 => "中等",
            1 => "弱",
            _ => "极弱",
        };

        var crackSeconds = Math.Pow(2, Math.Min(effectiveEntropy, 128)) / GuessesPerSecond;
        var crackTime = crackSeconds >= TimeSpan.MaxValue.TotalSeconds
            ? TimeSpan.MaxValue
            : TimeSpan.FromSeconds(crackSeconds);

        return new PasswordStrengthResult(score, label, Math.Round(entropy, 1), crackTime, suggestions);
    }
}
