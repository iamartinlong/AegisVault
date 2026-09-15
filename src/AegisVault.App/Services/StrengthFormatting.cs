using AegisVault.App.Localization;
using AegisVault.Core.Services;

namespace AegisVault.App.Services;

/// <summary>Human-friendly, localized formatting for password strength values.</summary>
public static class StrengthFormatting
{
    public static string FormatLabel(PasswordStrengthLabel label) => Loc.T(label switch
    {
        PasswordStrengthLabel.VeryStrong => "Strength_Label_VeryStrong",
        PasswordStrengthLabel.Strong => "Strength_Label_Strong",
        PasswordStrengthLabel.Fair => "Strength_Label_Fair",
        PasswordStrengthLabel.Weak => "Strength_Label_Weak",
        _ => "Strength_Label_VeryWeak",
    });

    public static string FormatAdvice(PasswordAdvice advice) => Loc.T(advice switch
    {
        PasswordAdvice.Empty => "Strength_Advice_Empty",
        PasswordAdvice.CommonPassword => "Strength_Advice_CommonPassword",
        PasswordAdvice.Repetitive => "Strength_Advice_Repetitive",
        PasswordAdvice.TooShort => "Strength_Advice_TooShort",
        PasswordAdvice.MixedCase => "Strength_Advice_MixedCase",
        PasswordAdvice.AddDigit => "Strength_Advice_AddDigit",
        _ => "Strength_Advice_AddSymbol",
    });

    public static string FormatCrackTime(TimeSpan crackTime)
    {
        if (crackTime <= TimeSpan.Zero)
        {
            return Loc.T("Strength_CrackInstant");
        }

        if (crackTime == TimeSpan.MaxValue)
        {
            return Loc.T("Strength_CrackOver1000Years");
        }

        var seconds = crackTime.TotalSeconds;
        if (seconds < 1)
        {
            return Loc.T("Strength_CrackInstant");
        }

        if (seconds < 60)
        {
            return Loc.Format("Strength_CrackSeconds", Math.Round(seconds, 0, MidpointRounding.AwayFromZero));
        }

        if (seconds < 3600)
        {
            return Loc.Format("Strength_CrackMinutes", Math.Round(seconds / 60, 0, MidpointRounding.AwayFromZero));
        }

        if (seconds < 86400)
        {
            return Loc.Format("Strength_CrackHours", Math.Round(seconds / 3600, 0, MidpointRounding.AwayFromZero));
        }

        var days = seconds / 86400;
        if (days < 365)
        {
            return Loc.Format("Strength_CrackDays", Math.Round(days, 0, MidpointRounding.AwayFromZero));
        }

        var years = days / 365;
        return years < 1000
            ? Loc.Format("Strength_CrackYears", Math.Round(years, 0, MidpointRounding.AwayFromZero))
            : Loc.T("Strength_CrackOver1000Years");
    }

    public static string FormatSummary(int score, PasswordStrengthLabel label, TimeSpan crackTime)
        => score <= 0 && crackTime <= TimeSpan.Zero
            ? string.Empty
            : Loc.Format("Strength_Summary", FormatLabel(label), FormatCrackTime(crackTime));

    public static string FormatInfo(double entropyBits) => Loc.Format("Strength_Info", entropyBits);
}
