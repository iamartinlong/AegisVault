namespace AegisVault.Core.Storage;

/// <summary>
/// Owner-only permissions for vault files on Unix. The default 0644 mode leaks
/// the existence/size/mtime of an otherwise encrypted vault to other users.
/// Windows keeps its own ACL defaults (the vault lives in the user profile).
/// </summary>
internal static class VaultFilePermissions
{
    internal const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Restricts the given paths to the owner (best effort).</summary>
    internal static void Restrict(params string[] paths)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetUnixFileMode(path, OwnerOnly);
                }
            }
            catch (Exception)
            {
                // Some file systems (or mounted volumes) do not support Unix
                // modes; the vault stays usable either way.
            }
        }
    }

    /// <summary>Restricts a vault file together with its SQLite side files.</summary>
    internal static void RestrictVault(string path)
        => Restrict(path, path + "-wal", path + "-shm");
}
