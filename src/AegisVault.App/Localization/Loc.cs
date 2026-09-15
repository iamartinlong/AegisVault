using System.Globalization;

namespace AegisVault.App.Localization;

/// <summary>
/// Static, AOT-safe localized string lookup. The language is resolved once at
/// startup from <see cref="AegisVault.Core.Models.AppPreferences.Language"/>;
/// changing it requires an application restart.
/// </summary>
public static class Loc
{
    public const string System = "system";
    public const string Chinese = "zh";
    public const string English = "en";

    private static string _language = Chinese;

    public static string Language => _language;

    public static void ApplyPreference(string? preference)
        => _language = Resolve(preference, CultureInfo.CurrentUICulture.Name);

    public static void ApplyPreference(string? preference, string systemCulture)
        => _language = Resolve(preference, systemCulture);

    /// <summary>Resolves a preference ("system"/"zh"/"en") to a supported language code.</summary>
    public static string Resolve(string? preference, string? systemCulture)
    {
        if (string.Equals(preference, English, StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        if (string.Equals(preference, Chinese, StringComparison.OrdinalIgnoreCase))
        {
            return Chinese;
        }

        return systemCulture?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true
            ? English
            : Chinese;
    }

    public static string T(string key) => Get(_language, key);

    public static string Format(string key, params object?[] arguments)
        => string.Format(CultureInfo.CurrentCulture, T(key), arguments);

    public static string Get(string language, string key)
        => TableFor(language).TryGetValue(key, out var value) ? value : key;

    public static IReadOnlyCollection<string> Keys(string language) => TableFor(language).Keys;

    private static Dictionary<string, string> TableFor(string language)
        => string.Equals(language, English, StringComparison.OrdinalIgnoreCase)
            ? EnStrings.Table
            : ZhStrings.Table;
}
