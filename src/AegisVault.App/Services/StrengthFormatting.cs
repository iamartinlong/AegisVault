using System.Globalization;

namespace AegisVault.App.Services;

/// <summary>Human-friendly formatting for password strength values.</summary>
public static class StrengthFormatting
{
    public static string FormatCrackTime(TimeSpan crackTime)
    {
        if (crackTime <= TimeSpan.Zero)
        {
            return "立即";
        }

        if (crackTime == TimeSpan.MaxValue)
        {
            return "超过 1000 年";
        }

        var seconds = crackTime.TotalSeconds;
        if (seconds < 1)
        {
            return "立即";
        }

        if (seconds < 60)
        {
            return $"{Math.Round(seconds, 0, MidpointRounding.AwayFromZero)} 秒";
        }

        if (seconds < 3600)
        {
            return $"{Math.Round(seconds / 60, 0, MidpointRounding.AwayFromZero)} 分钟";
        }

        if (seconds < 86400)
        {
            return $"{Math.Round(seconds / 3600, 0, MidpointRounding.AwayFromZero)} 小时";
        }

        var days = seconds / 86400;
        if (days < 365)
        {
            return $"{Math.Round(days, 0, MidpointRounding.AwayFromZero)} 天";
        }

        var years = days / 365;
        return years < 1000
            ? $"{Math.Round(years, 0, MidpointRounding.AwayFromZero)} 年"
            : "超过 1000 年";
    }

    public static string FormatSummary(int score, string label, TimeSpan crackTime)
        => score <= 0 && crackTime <= TimeSpan.Zero
            ? string.Empty
            : $"{label} · 离线破解约 {FormatCrackTime(crackTime)}";

    public static string FormatInfo(double entropyBits)
        => string.Create(CultureInfo.InvariantCulture, $"熵 {entropyBits:0} bits");
}
