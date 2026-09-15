using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class CategoryTests : IDisposable
{
    private static readonly byte[] Password = "master password"u8.ToArray();

    private static readonly VaultOptions FastOptions = new()
    {
        Kdf = new KdfParameters
        {
            Algorithm = KdfParameters.AlgorithmArgon2id,
            Iterations = 3,
            MemoryBytes = 8L * 1024 * 1024,
        },
    };

    private readonly string _directory;

    public CategoryTests()
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
    public void CategoryCrudPersistsAcrossReopen()
    {
        var path = Path.Combine(_directory, "vault.aegis");
        Guid categoryId;

        using (var vault = VaultService.CreateNew(path, Password, FastOptions))
        {
            var category = vault.AddCategory("Work");
            categoryId = category.Id;

            var entry = vault.AddEntry(new PasswordEntry { Title = "GitHub", CategoryId = categoryId });
            Assert.Equal(categoryId, entry.CategoryId);

            Assert.Single(vault.Categories);
            Assert.True(vault.RenameCategory(categoryId, "Job"));
            Assert.Equal("Job", vault.Categories[0].Name);
        }

        using (var vault = VaultService.Open(path))
        {
            Assert.Equal(VaultUnlockStatus.Success, vault.Unlock(Password));
            Assert.Equal("Job", vault.Categories.Single().Name);
            Assert.Equal(categoryId, vault.Entries.Single().CategoryId);

            Assert.True(vault.DeleteCategory(categoryId));
            Assert.Empty(vault.Categories);
            Assert.Null(vault.Entries.Single().CategoryId);
        }
    }

    [Fact]
    public void RejectsEmptyAndDuplicateNames()
    {
        using var vault = VaultService.CreateNew(Path.Combine(_directory, "names.aegis"), Password, FastOptions);
        vault.AddCategory("Work");

        Assert.Throws<ArgumentException>(() => vault.AddCategory("   "));
        Assert.Throws<ArgumentException>(() => vault.AddCategory("work"));

        var personal = vault.AddCategory("Personal");
        Assert.Throws<ArgumentException>(() => vault.RenameCategory(personal.Id, "WORK"));

        // Renaming to its own name is allowed.
        Assert.True(vault.RenameCategory(personal.Id, "Personal"));
    }

    [Fact]
    public void LegacyVaultHasNoCategories()
    {
        var path = Path.Combine(_directory, "legacy.aegis");
        using (var vault = VaultService.CreateNew(path, Password, FastOptions))
        {
            vault.AddEntry(new PasswordEntry { Title = "A" });
        }

        using var reopened = VaultService.Open(path);
        Assert.Equal(VaultUnlockStatus.Success, reopened.Unlock(Password));
        Assert.Empty(reopened.Categories);
        Assert.Null(reopened.Entries.Single().CategoryId);
    }

    [Fact]
    public void LockClearsCategories()
    {
        using var vault = VaultService.CreateNew(Path.Combine(_directory, "lock.aegis"), Password, FastOptions);
        vault.AddCategory("Work");

        vault.Lock();

        Assert.Empty(vault.Categories);
    }
}
