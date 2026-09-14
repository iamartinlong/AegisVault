using AegisVault.Core.Models;

namespace AegisVault.Core.Services;

public sealed record VaultHealthReport(
    int TotalEntries,
    int WeakCount,
    int ReusedCount,
    int OldCount,
    IReadOnlySet<Guid> WeakEntryIds,
    IReadOnlySet<Guid> ReusedEntryIds)
{
    public bool HasIssues => WeakCount > 0 || ReusedCount > 0;

    /// <summary>Distinct entries with any issue (weak or reused — no double counting).</summary>
    public int IssueCount
    {
        get
        {
            var ids = new HashSet<Guid>(WeakEntryIds);
            ids.UnionWith(ReusedEntryIds);
            return ids.Count;
        }
    }

    /// <summary>0–100; entries with a weak or reused password drag it down.</summary>
    public int HealthScore => TotalEntries == 0
        ? 100
        : (int)Math.Round(100d * (TotalEntries - WeakEntryIds.Count - ReusedEntryIds.Count) / TotalEntries);
}

/// <summary>
/// Security health analysis over the decrypted entries: weak passwords
/// (estimator score below the threshold) and passwords reused across entries.
/// </summary>
public static class VaultHealth
{
    /// <summary>Scores of 0–2 (极弱/弱) count as weak.</summary>
    public const int WeakScoreThreshold = 2;

    private static readonly TimeSpan OldAge = TimeSpan.FromDays(365);

    public static VaultHealthReport Analyze(IEnumerable<PasswordEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var list = entries.ToList();
        var weak = new HashSet<Guid>();
        var reused = new HashSet<Guid>();

        var byPassword = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);
        foreach (var entry in list)
        {
            if (string.IsNullOrEmpty(entry.Password))
            {
                continue;
            }

            if (PasswordStrengthEstimator.Evaluate(entry.Password).Score <= WeakScoreThreshold)
            {
                weak.Add(entry.Id);
            }

            if (!byPassword.TryGetValue(entry.Password, out var ids))
            {
                ids = [];
                byPassword[entry.Password] = ids;
            }

            ids.Add(entry.Id);
        }

        foreach (var ids in byPassword.Values)
        {
            if (ids.Count > 1)
            {
                reused.UnionWith(ids);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var oldCount = list.Count(entry => now - entry.UpdatedAt > OldAge);

        return new VaultHealthReport(
            list.Count,
            weak.Count,
            reused.Count,
            oldCount,
            weak,
            reused);
    }
}
