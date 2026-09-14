using System.Text;
using AegisVault.Core.Models;

namespace AegisVault.Core.Services;

public sealed record ImportResult(int Imported, int Skipped);

/// <summary>
/// Imports entries from CSV exports. Supported formats:
/// Bitwarden (folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp)
/// and a generic name/username/password layout. The format is auto-detected
/// from the header row.
/// </summary>
public static class VaultCsvImporter
{
    public static ImportResult Import(VaultService vault, string csv)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(csv);

        var rows = ParseCsv(csv);
        if (rows.Count == 0)
        {
            return new ImportResult(0, 0);
        }

        var headers = rows[0]
            .Select(static header => header.Trim().TrimStart('\uFEFF'))
            .ToList();

        var isBitwarden = headers.Contains("login_username", StringComparer.OrdinalIgnoreCase) ||
                          headers.Contains("login_password", StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            map[headers[i].ToLowerInvariant()] = i;
        }

        var imported = 0;
        var skipped = 0;

        for (var row = 1; row < rows.Count; row++)
        {
            var fields = rows[row];
            if (fields.All(static field => field.Length == 0))
            {
                skipped++;
                continue;
            }

            var title = Get(fields, map, isBitwarden ? ["name"] : ["name", "title"]);
            var username = Get(fields, map, isBitwarden ? ["login_username"] : ["username", "user", "login"]);
            var password = Get(fields, map, isBitwarden ? ["login_password"] : ["password", "pass"]);
            var url = Get(fields, map, isBitwarden ? ["login_uri"] : ["url", "uri", "website", "site"]);
            var notes = Get(fields, map, isBitwarden ? ["notes", "login_notes"] : ["notes", "note"]);
            var totp = Get(fields, map, isBitwarden ? ["login_totp"] : ["totp", "otp"]);
            var folder = Get(fields, map, ["folder"]);
            var favorite = Get(fields, map, ["favorite"]);

            if (title.Length == 0 && username.Length == 0 && password.Length == 0)
            {
                skipped++;
                continue;
            }

            var tags = new List<string>();
            if (folder.Length > 0)
            {
                tags.Add(folder);
            }

            vault.AddEntry(new PasswordEntry
            {
                Title = title.Length == 0 ? username : title,
                Username = username,
                Password = password,
                Url = url,
                Notes = notes,
                TotpSecret = totp,
                Tags = tags,
                IsFavorite = favorite.Equals("true", StringComparison.OrdinalIgnoreCase),
            });
            imported++;
        }

        return new ImportResult(imported, skipped);
    }

    private static string Get(List<string> fields, Dictionary<string, int> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out var index) && index < fields.Count)
            {
                return fields[index].Trim();
            }
        }

        return string.Empty;
    }

    /// <summary>RFC-4180-style CSV parsing: quoted fields, escaped double quotes, CRLF/LF.</summary>
    public static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        var field = new StringBuilder();
        var row = new List<string>();
        var inQuotes = false;
        var fieldStarted = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    fieldStarted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    fieldStarted = false;
                    break;
                default:
                    field.Append(c);
                    fieldStarted = true;
                    break;
            }
        }

        if (field.Length > 0 || fieldStarted || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
