namespace AegisVault.Core.Models;

/// <summary>
/// The persisted category colour vocabulary: either one of the named palette
/// keys (theme-aware in the UI) or a literal <c>#RRGGBB</c> value. Kept in Core
/// because it is part of the stored settings contract.
/// </summary>
public static class CategoryColors
{
    /// <summary>Canonical order used by the picker palette.</summary>
    public static readonly string[] PaletteKeys =
    [
        "red",
        "orange",
        "gold",
        "green",
        "cyan",
        "blue",
        "purple",
        "magenta",
    ];

    /// <summary>
    /// Canonicalizes a stored colour: palette keys are lower-cased, hex values
    /// are expanded to <c>#RRGGBB</c> and upper-cased; anything else is dropped
    /// (returns an empty string meaning "unset").
    /// </summary>
    public static string Normalize(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return string.Empty;
        }

        if (trimmed[0] == '#')
        {
            var hex = trimmed[1..];
            if (hex.Length == 3 && hex.All(Uri.IsHexDigit))
            {
                return $"#{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}".ToUpperInvariant();
            }

            return hex.Length == 6 && hex.All(Uri.IsHexDigit)
                ? ("#" + hex).ToUpperInvariant()
                : string.Empty;
        }

        var key = trimmed.ToLowerInvariant();
        return PaletteKeys.Contains(key) ? key : string.Empty;
    }

    /// <summary>True when the stored value is a literal hex colour.</summary>
    public static bool IsHex(string? value)
        => value is { Length: 7 } && value[0] == '#';
}
