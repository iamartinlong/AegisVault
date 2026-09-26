using System.Text;
using AegisVault.Core.Models;

namespace AegisVault.Core.Services;

/// <summary>
/// Writes entries as a CSV document: Bitwarden-compatible columns plus a few
/// extra columns (phone/email/app_id/secret/api_key/urls) that
/// <see cref="VaultCsvImporter"/> reads back, so an export/import round-trip
/// through this application only loses custom fields.
/// <para>
/// The output is <b>plaintext</b>: it contains passwords, TOTP secrets and API
/// keys in the clear. Callers must warn the user before writing it to disk.
/// </para>
/// </summary>
public static class VaultCsvExporter
{
    /// <summary>Column order of the produced document.</summary>
    public static readonly string[] Headers =
    [
        "folder",
        "favorite",
        "type",
        "name",
        "notes",
        "fields",
        "reprompt",
        "login_uri",
        "login_username",
        "login_password",
        "login_totp",
        "phone",
        "email",
        "app_id",
        "secret",
        "api_key",
        "urls",
    ];

    /// <summary>
    /// Renders every live entry (soft-deleted ones are never exported) as CSV.
    /// </summary>
    /// <param name="entries">Entries to export; entries with a <c>DeletedAt</c> are skipped.</param>
    /// <param name="categories">Used to resolve the category name written to the <c>folder</c> column.</param>
    public static string Export(IEnumerable<PasswordEntry> entries, IEnumerable<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(categories);

        var categoryNames = categories
            .GroupBy(category => category.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var builder = new StringBuilder();
        AppendRow(builder, Headers);

        foreach (var entry in entries)
        {
            if (entry.DeletedAt is not null)
            {
                continue;
            }

            var urls = EffectiveUrls(entry);
            AppendRow(builder,
            [
                entry.CategoryId is { } categoryId && categoryNames.TryGetValue(categoryId, out var name)
                    ? name
                    : string.Empty,
                entry.IsFavorite ? "1" : string.Empty,
                "login",
                entry.Title,
                entry.Notes,
                string.Empty, // Bitwarden "fields": custom fields have no compatible layout
                "0",
                urls.Count > 0 ? urls[0] : string.Empty,
                entry.Username,
                entry.Password,
                entry.TotpSecret,
                entry.Phone,
                entry.Email,
                entry.AppId,
                entry.Secret,
                entry.ApiKey,
                string.Join('\n', urls),
            ]);
        }

        return builder.ToString();
    }

    /// <summary>All addresses of an entry, newest storage form first.</summary>
    private static List<string> EffectiveUrls(PasswordEntry entry)
    {
        // Source-generated JSON leaves new collection members null for payloads
        // written before they existed, so never assume non-null.
        if (entry.Urls is { Count: > 0 })
        {
            return entry.Urls.Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
        }

        return string.IsNullOrWhiteSpace(entry.Url) ? [] : [entry.Url];
    }

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendValue(builder, values[i]);
        }

        builder.Append("\r\n");
    }

    /// <summary>RFC-4180 field encoding: quote when the value holds a delimiter, quote or newline.</summary>
    private static void AppendValue(StringBuilder builder, string value)
    {
        var text = value ?? string.Empty;
        var needsQuotes = text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r');
        if (!needsQuotes)
        {
            builder.Append(text);
            return;
        }

        builder.Append('"');
        foreach (var character in text)
        {
            if (character == '"')
            {
                builder.Append('"');
            }

            builder.Append(character);
        }

        builder.Append('"');
    }
}
