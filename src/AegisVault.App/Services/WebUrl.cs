namespace AegisVault.App.Services;

/// <summary>
/// Normalizes and validates web addresses entered by the user. Only http/https
/// links are accepted; a missing scheme is completed with <c>https://</c>.
/// </summary>
public static class WebUrl
{
    /// <summary>
    /// Trims the input and adds an <c>https://</c> scheme when it is missing.
    /// Returns an empty string for empty input and <c>null</c> when the value is
    /// not a usable web address.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        // Require a dotted host (or localhost) so plain words like "not-a-url"
        // are rejected instead of being turned into https://not-a-url.
        var host = uri.Host;
        if (host.Length == 0 ||
            (!host.Contains('.') && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return value;
    }
}
