using AegisVault.Core.Models;

namespace AegisVault.Core.Storage;

/// <summary>
/// Load-time normalization and version upgrades for decrypted payloads.
/// <para>
/// Source-generated <c>System.Text.Json</c> contexts do <b>not</b> apply property
/// initializers for collection/object properties that are missing from older
/// payloads (they deserialize as <c>null</c>, unlike the reflection-based
/// serializer). Every model that can be loaded from disk therefore passes
/// through <see cref="Normalize(PasswordEntry)"/> and friends, and version-specific
/// changes are applied by <see cref="UpgradeEntry"/>.
/// </para>
/// <para>
/// Rules for future changes (see <c>docs/数据迁移设计.md</c>):
/// add-field: bump the payload version + extend <see cref="UpgradeEntry"/> +
/// extend <see cref="Normalize(PasswordEntry)"/> + extend the contract test.
/// </para>
/// </summary>
internal static class ModelMigrations
{
    /// <summary>Oldest entry payload version this application can still read.</summary>
    public const int OldestSupportedEntryVersion = 1;

    /// <summary>
    /// Applies version-specific upgrades to a decrypted entry payload.
    /// Always normalizes afterwards so consumers can rely on non-null members.
    /// </summary>
    public static PasswordEntry UpgradeEntry(PasswordEntry entry, int fromVersion)
    {
        if (fromVersion < OldestSupportedEntryVersion)
        {
            throw new InvalidDataException($"Entry format version {fromVersion} is not supported.");
        }

        // v1 -> v2: collection members (Tags/Urls/CustomFields) may be missing
        // from the payload; v2 guarantees non-null collections.
        // v2 -> v3: added scalar credential fields (Phone/AppId/Secret/ApiKey);
        // older payloads simply lack them and normalize to empty strings.
        // v3 -> v4: added the scalar Email field; same story.
        return Normalize(entry);
    }

    /// <summary>Fills in defaults for members that older payloads may lack.</summary>
    public static PasswordEntry Normalize(PasswordEntry entry) => entry with
    {
        Title = entry.Title ?? string.Empty,
        Username = entry.Username ?? string.Empty,
        Password = entry.Password ?? string.Empty,
        Url = entry.Url ?? string.Empty,
        Notes = entry.Notes ?? string.Empty,
        TotpSecret = entry.TotpSecret ?? string.Empty,
        Phone = entry.Phone ?? string.Empty,
        Email = entry.Email ?? string.Empty,
        AppId = entry.AppId ?? string.Empty,
        Secret = entry.Secret ?? string.Empty,
        ApiKey = entry.ApiKey ?? string.Empty,
        Tags = entry.Tags is null ? [] : entry.Tags,
        Urls = entry.Urls is null ? [] : entry.Urls,
        CustomFields = entry.CustomFields is null ? [] : entry.CustomFields,
    };

    /// <summary>Fills in defaults for members that older payloads may lack.</summary>
    public static UserConfig Normalize(UserConfig config) => config with
    {
        Generator = config.Generator is null ? new PasswordGeneratorOptions() : config.Generator,
    };

    /// <summary>Fills in defaults for members that older payloads may lack.</summary>
    public static Category Normalize(Category category) => category with
    {
        Name = category.Name ?? string.Empty,
    };
}
