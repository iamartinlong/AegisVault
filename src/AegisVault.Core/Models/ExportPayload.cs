namespace AegisVault.Core.Models;

/// <summary>
/// Body of an encrypted export: the entries and categories of a vault, wrapped in
/// a versioned envelope (mirrors <see cref="CategoriesPayload"/> so a payload
/// written by a newer release is reported as unsupported instead of misread).
/// </summary>
public sealed record ExportPayload
{
    /// <summary>Oldest payload this application can still read.</summary>
    public const int OldestSupportedVersion = 1;

    /// <summary>Current payload version.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>Entries; <c>null</c> when the member is missing from the payload.</summary>
    public List<PasswordEntry>? Entries { get; init; }

    /// <summary>Categories; <c>null</c> when the member is missing from the payload.</summary>
    public List<Category>? Categories { get; init; }
}

/// <summary>
/// On-disk envelope of an encrypted export. Only the metadata is cleartext; the
/// payload is AES-256-GCM sealed under a key derived from a user passphrase, so
/// the file can be restored on another machine without this vault's DEK.
/// </summary>
public sealed record EncryptedExport
{
    /// <summary>Marker identifying an AegisVault encrypted export.</summary>
    public const string FormatIdentifier = "aegisvault-export";

    /// <summary>Oldest envelope this application can still read.</summary>
    public const int OldestSupportedVersion = 1;

    /// <summary>Current envelope version.</summary>
    public const int CurrentVersion = 1;

    public string Format { get; init; } = FormatIdentifier;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>Key derivation parameters the passphrase was stretched with.</summary>
    public KdfParameters? Kdf { get; init; }

    /// <summary>Base64 salt used for the key derivation.</summary>
    public string Salt { get; init; } = string.Empty;

    /// <summary>Base64 AES-GCM nonce.</summary>
    public string Nonce { get; init; } = string.Empty;

    /// <summary>Base64 ciphertext of the serialized <see cref="ExportPayload"/>.</summary>
    public string Ciphertext { get; init; } = string.Empty;

    /// <summary>Base64 AES-GCM authentication tag.</summary>
    public string Tag { get; init; } = string.Empty;
}
