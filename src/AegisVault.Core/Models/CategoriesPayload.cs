namespace AegisVault.Core.Models;

/// <summary>
/// Versioned envelope for the encrypted <c>categories</c> setting.
/// <para>
/// Releases up to v0.2.0 wrote a bare <c>List&lt;Category&gt;</c> under the
/// <c>AegisVault|settings|categories|v2</c> AAD, so the payload carried no
/// version of its own and a newer payload could only be detected by the AAD
/// failing. The envelope makes the version explicit: it participates in both the
/// AAD (<c>…|categories|v3</c>) and the body, so a payload written by a newer
/// application is reported as unsupported instead of being read as empty.
/// </para>
/// </summary>
public sealed record CategoriesPayload
{
    /// <summary>Oldest payload this application can still read (bare list).</summary>
    public const int OldestSupportedVersion = 1;

    /// <summary>Current payload version: versioned envelope with the category tree.</summary>
    public const int CurrentVersion = 3;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>
    /// Categories; <c>null</c> when the member is missing from the payload
    /// (source-generated JSON does not run property initializers).
    /// </summary>
    public List<Category>? Categories { get; init; }
}
