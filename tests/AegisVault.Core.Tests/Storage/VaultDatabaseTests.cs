using AegisVault.Core.Storage;
using Xunit;

namespace AegisVault.Core.Tests.Storage;

public sealed class VaultDatabaseTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "aegis-db-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void UsesWalWithNormalSynchronousMode()
    {
        using var database = VaultDatabase.OpenOrCreate(Path.Combine(_directory, "vault.db"));

        // NORMAL (1) is the documented-safe pairing for WAL and avoids an
        // fsync per commit on bulk writes.
        Assert.Equal(1, database.SynchronousMode);
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
