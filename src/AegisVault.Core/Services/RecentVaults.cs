namespace AegisVault.Core.Services;

/// <summary>
/// Rules for the "recent vaults" shortcut list kept in the plaintext
/// preferences: newest first, trimmed, de-duplicated (case-insensitive) and
/// capped. Kept pure so both the store (normalisation) and the shell (adding an
/// opened vault) agree on the shape.
/// </summary>
public static class RecentVaults
{
    public const int MaxCount = 3;

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? paths)
    {
        var result = new List<string>();
        if (paths is null)
        {
            return result;
        }

        foreach (var path in paths)
        {
            var trimmed = path?.Trim();
            if (string.IsNullOrEmpty(trimmed) || Contains(result, trimmed))
            {
                continue;
            }

            result.Add(trimmed);
            if (result.Count == MaxCount)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>Returns the list with <paramref name="path"/> moved to the front.</summary>
    public static IReadOnlyList<string> Add(IEnumerable<string>? paths, string? path)
    {
        var trimmed = path?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Normalize(paths);
        }

        return Normalize([trimmed, .. paths ?? []]);
    }

    private static bool Contains(List<string> paths, string candidate)
        => paths.Exists(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase));
}
