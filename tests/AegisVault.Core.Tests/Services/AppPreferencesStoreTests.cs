using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class AppPreferencesStoreTests : IDisposable
{
    private readonly string _directory;

    public AppPreferencesStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "aegis-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
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

    [Fact]
    public void RoundTripsPreferences()
    {
        var path = Path.Combine(_directory, "app.json");
        var store = new AppPreferencesStore(path);

        store.Save(new AppPreferences
        {
            LastVaultPath = Path.Combine(_directory, "vault.aegis"),
            Theme = "dark",
        });

        var loaded = new AppPreferencesStore(path).Load();

        Assert.Equal(Path.Combine(_directory, "vault.aegis"), loaded.LastVaultPath);
        Assert.Equal("dark", loaded.Theme);
    }

    [Fact]
    public void MissingFileReturnsDefaults()
    {
        var store = new AppPreferencesStore(Path.Combine(_directory, "missing.json"));

        var loaded = store.Load();

        Assert.Null(loaded.LastVaultPath);
        Assert.Equal("system", loaded.Theme);
    }

    [Fact]
    public void CorruptFileReturnsDefaults()
    {
        var path = Path.Combine(_directory, "app.json");
        File.WriteAllText(path, "{ not valid json !!");

        var loaded = new AppPreferencesStore(path).Load();

        Assert.Null(loaded.LastVaultPath);
        Assert.Equal("system", loaded.Theme);
    }

    [Fact]
    public void SaveCreatesDirectory()
    {
        var path = Path.Combine(_directory, "nested", "deeper", "app.json");

        new AppPreferencesStore(path).Save(new AppPreferences { LastVaultPath = "x" });

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void FileContainsNoSecrets()
    {
        var path = Path.Combine(_directory, "app.json");
        new AppPreferencesStore(path).Save(new AppPreferences { LastVaultPath = @"C:\v\vault.aegis" });

        var content = File.ReadAllText(path);

        Assert.Contains("lastVaultPath", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dek", content, StringComparison.OrdinalIgnoreCase);
    }
}
