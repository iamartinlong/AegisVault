using AegisVault.Core.Models;

namespace AegisVault.Core.Services;

public sealed record VaultHealthReport(
    int TotalEntries,
    int WeakCount,
    int ReusedCount,
    IReadOnlySet<Guid> WeakEntryIds,
    IReadOnlySet<Guid> ReusedEntryIds,
    IReadOnlySet<Guid> OldEntryIds)
{
    /// <summary>Entries whose UpdatedAt is older than a year.</summary>
    public int OldCount => OldEntryIds.Count;

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
/// (estimator score below the threshold), passwords reused across entries,
/// and stale entries (last updated more than a year ago).
/// </summary>
public static class VaultHealth
{
    /// <summary>Scores of 0–2 (VeryWeak/Weak) count as weak.</summary>
    public const int WeakScoreThreshold = 2;

    private static readonly TimeSpan OldAge = TimeSpan.FromDays(365);

    public static VaultHealthReport Analyze(IEnumerable<PasswordEntry> entries, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var list = entries.ToList();
        var weak = new HashSet<Guid>();
        var reused = new HashSet<Guid>();
        var old = new HashSet<Guid>();

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

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        foreach (var entry in list)
        {
            if (now - entry.UpdatedAt > OldAge)
            {
                old.Add(entry.Id);
            }
        }

        return new VaultHealthReport(
            list.Count,
            weak.Count,
            reused.Count,
            weak,
            reused,
            old);
    }
}
