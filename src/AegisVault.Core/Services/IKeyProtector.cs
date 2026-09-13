namespace AegisVault.Core.Services;

/// <summary>
/// OS-level key protection (DPAPI, Keychain, libsecret). Implementations live
/// in the platform layer; the vault layer only sees this abstraction.
/// </summary>
public interface IKeyProtector
{
    /// <summary>Stable identifier persisted with the protected blob.</summary>
    string Id { get; }

    /// <summary>Whether this protector can be used on the current machine.</summary>
    bool IsAvailable { get; }

    byte[] Protect(ReadOnlySpan<byte> data);

    byte[] Unprotect(ReadOnlySpan<byte> data);
}
