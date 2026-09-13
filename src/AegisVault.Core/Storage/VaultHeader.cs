using System.Text;
using AegisVault.Core.Models;

namespace AegisVault.Core.Storage;

/// <summary>
/// Persisted vault metadata. The header is stored as plain SQLite columns
/// (it contains no secrets); <see cref="WrappedDek"/> is bound to the other
/// fields through the AES-GCM associated data.
/// </summary>
public sealed class VaultHeader
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required KdfParameters Kdf { get; init; }

    public required byte[] Salt { get; init; }

    public required byte[] WrappedDek { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public byte[] ComputeAssociatedData()
        => ComputeAssociatedData(FormatVersion, Kdf, Salt);

    /// <summary>
    /// Deterministic associated data binding the wrapped DEK to the format
    /// version and KDF parameters (prevents parameter downgrade tampering).
    /// </summary>
    public static byte[] ComputeAssociatedData(int formatVersion, KdfParameters kdf, ReadOnlySpan<byte> salt)
    {
        var canonical = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"AegisVault|header|v{formatVersion}|{kdf.Algorithm}|{kdf.Iterations}|{kdf.MemoryBytes}|{kdf.KeySize}|{Convert.ToBase64String(salt)}");
        return Encoding.UTF8.GetBytes(canonical);
    }
}
