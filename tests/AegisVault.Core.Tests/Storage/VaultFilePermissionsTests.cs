using AegisVault.Core.Services;
using AegisVault.Core.Storage;
using Xunit;

namespace AegisVault.Core.Tests.Storage;

public sealed class VaultFilePermissionsTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "aegis-perm-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void VaultDatabaseUsesOwnerOnlyPermissionsOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "vault.aegis");
        using (VaultDatabase.OpenOrCreate(path))
        {
        }

        Assert.Equal(VaultFilePermissions.OwnerOnly, File.GetUnixFileMode(path));
    }

    [Fact]
    public void BackupUsesOwnerOnlyPermissionsOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "vault.aegis");
        using (var vault = VaultService.CreateNew(path, "master password"u8))
        {
            vault.SaveBackup();
        }

        Assert.Equal(VaultFilePermissions.OwnerOnly, File.GetUnixFileMode(path + ".bak"));
    }

    [Fact]
    public void MissingPathsAreIgnored()
    {
        VaultFilePermissions.Restrict(Path.Combine(_directory, "does-not-exist.aegis"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
