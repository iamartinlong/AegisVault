namespace AegisVault.Core.Services;

/// <summary>
/// The vault file was written by a newer application version (header format
/// version, entry payload version or database schema version) and must not be
/// opened by this build.
/// </summary>
public sealed class UnsupportedVaultVersionException(string message) : Exception(message);
