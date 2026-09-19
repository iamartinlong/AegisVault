using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AegisVault.Core.Crypto;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using AegisVault.Core.Storage;
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
    public void ColorsPersistAndNormalizeAcrossReopen()
    {
        var path = Path.Combine(_directory, "colors.aegis");
        Guid paletteId;
        Guid customId;

        using (var vault = VaultService.CreateNew(path, Password, FastOptions))
        {
            paletteId = vault.AddCategory("Palette", "blue").Id;
            customId = vault.AddCategory("Custom", "#3b82f6").Id;
            vault.AddCategory("Unset");

            // Case/format canonicalization on the way in.
            Assert.True(vault.SetCategoryColor(paletteId, "  PURPLE "));
            Assert.True(vault.SetCategoryColor(customId, "#abc"));
        }

        using (var vault = VaultService.Open(path))
        {
            Assert.Equal(VaultUnlockStatus.Success, vault.Unlock(Password));

            Assert.Equal("purple", vault.Categories.Single(c => c.Id == paletteId).Color);
            Assert.Equal("#AABBCC", vault.Categories.Single(c => c.Id == customId).Color);
            Assert.Equal(string.Empty, vault.Categories.Single(c => c.Name == "Unset").Color);
        }
    }

    [Fact]
    public void UnknownColorsAndMissingIdsAreIgnored()
    {
        using var vault = VaultService.CreateNew(Path.Combine(_directory, "invalid.aegis"), Password, FastOptions);
        var category = vault.AddCategory("Work", "not-a-color");

        Assert.Equal(string.Empty, category.Color);

        Assert.True(vault.SetCategoryColor(category.Id, "#12345"));
        Assert.Equal(string.Empty, vault.Categories.Single().Color);

        Assert.False(vault.SetCategoryColor(Guid.NewGuid(), "red"));
    }

    [Fact]
    public void LegacyCategoriesPayloadIsReadAndRewrittenWithCurrentAad()
    {
        // Build the vault the way the first release wrote it: a bare category
        // list encrypted with the v1 AAD (no colour, no hierarchy).
        var path = Path.Combine(_directory, "legacy-categories.aegis");
        var categoryId = Guid.NewGuid();
        var legacyJson = JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            new { Id = categoryId, Name = "Legacy", CreatedAt = DateTimeOffset.UtcNow },
        });

        var dek = WriteVaultWithCategoriesBlob(path, legacyJson, 1);
        try
        {
            using (var vault = VaultService.Open(path))
            {
                Assert.Equal(VaultUnlockStatus.Success, vault.Unlock(Password));
                var category = Assert.Single(vault.Categories);
                Assert.Equal("Legacy", category.Name);
                Assert.Equal(string.Empty, category.Color);
                Assert.Null(category.ParentId);
                Assert.False(vault.CategoriesUnreadable);
            }

            // The unlock rewrites the blob as the current envelope.
            var rewritten = ReadCategoriesBlob(path, dek, CategoriesPayload.CurrentVersion);
            var text = Encoding.UTF8.GetString(rewritten);
            Assert.Contains($"\"Version\":{CategoriesPayload.CurrentVersion}", text);
            Assert.Contains("\"Color\"", text);
            Assert.Contains("\"ParentId\"", text);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    [Fact]
    public void V2BareListIsReadAndRewrittenAsTheVersionedEnvelope()
    {
        var path = Path.Combine(_directory, "v2-categories.aegis");
        var legacyJson = JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            new Category { Id = Guid.NewGuid(), Name = "Work", Color = "blue" },
        });

        var dek = WriteVaultWithCategoriesBlob(path, legacyJson, 2);
        try
        {
            using var vault = VaultService.Open(path);
            Assert.Equal(VaultUnlockStatus.Success, vault.Unlock(Password));
            Assert.Equal("blue", vault.Categories.Single().Color);

            var rewritten = Encoding.UTF8.GetString(ReadCategoriesBlob(path, dek, CategoriesPayload.CurrentVersion));
            Assert.Contains($"\"Version\":{CategoriesPayload.CurrentVersion}", rewritten);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    [Fact]
    public void CategoryTreeRoundTripsThroughThePayload()
    {
        var path = Path.Combine(_directory, "tree.aegis");
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var payload = new CategoriesPayload
        {
            Categories =
            [
                new Category { Id = rootId, Name = "Work", Color = "green" },
                new Category { Id = childId, Name = "Servers", ParentId = rootId },
            ],
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, VaultJsonContext.Default.CategoriesPayload);

        var dek = WriteVaultWithCategoriesBlob(path, json, CategoriesPayload.CurrentVersion);
        try
        {
            using var vault = VaultService.Open(path);
            Assert.Equal(VaultUnlockStatus.Success, vault.Unlock(Password));

            Assert.Equal(rootId, vault.Categories.Single(c => c.Id == childId).ParentId);
            Assert.Null(vault.Categories.Single(c => c.Id == rootId).ParentId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    [Fact]
    public void NewerCategoriesPayloadIsReportedAsUnsupportedVersion()
    {
        // A future release writes the same envelope shape with a higher version.
        var path = Path.Combine(_directory, "newer-categories.aegis");
        var payload = new CategoriesPayload
        {
            Version = CategoriesPayload.CurrentVersion + 1,
            Categories = [new Category { Name = "FromTheFuture" }],
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, VaultJsonContext.Default.CategoriesPayload);

        var dek = WriteVaultWithCategoriesBlob(path, json, CategoriesPayload.CurrentVersion);
        try
        {
            using var vault = VaultService.Open(path);
            Assert.Equal(VaultUnlockStatus.UnsupportedVersion, vault.Unlock(Password));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    [Fact]
    public void UnreadableCategoriesBlobUnlocksWithAWarning()
    {
        var path = Path.Combine(_directory, "unreadable-categories.aegis");
        var json = JsonSerializer.SerializeToUtf8Bytes(new[] { new Category { Name = "Hidden" } });
        var dek = WriteVaultWithCategoriesBlob(path, json, 9);
        try
        {
            using var vault = VaultService.Open(path);
            Assert.Equal(VaultUnlockStatus.Success, vault.Unlock(Password));

            Assert.Empty(vault.Categories);
            Assert.True(vault.CategoriesUnreadable);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>
    /// Creates a vault whose categories blob is encrypted under
    /// <c>AegisVault|settings|categories|v{version}</c> and returns the raw DEK.
    /// </summary>
    private static byte[] WriteVaultWithCategoriesBlob(string path, byte[] json, int version)
    {
        var kdf = FastOptions.Kdf!;
        var salt = RandomNumberGenerator.GetBytes(kdf.SaltSize);
        using var kek = KeyDerivationFactory.Create(kdf.Algorithm).DeriveKey(Password, salt, kdf);
        var dek = RandomNumberGenerator.GetBytes(KeyEnvelope.DekSize);

        using var database = VaultDatabase.OpenOrCreate(path);
        var now = DateTimeOffset.UtcNow;
        database.WriteMeta(new VaultHeader
        {
            FormatVersion = VaultHeader.CurrentFormatVersion,
            Kdf = kdf,
            Salt = salt,
            WrappedDek = KeyEnvelope.Wrap(
                kek.ReadOnlySpan,
                dek,
                VaultHeader.ComputeAssociatedData(VaultHeader.CurrentFormatVersion, kdf, salt)),
            CreatedAt = now,
            UpdatedAt = now,
        });

        var (nonce, ciphertext, tag) = AesGcmCipher.Encrypt(
            dek,
            json,
            Encoding.UTF8.GetBytes($"AegisVault|settings|categories|v{version}"));
        database.WriteSetting("categories", nonce, ciphertext, tag);
        return dek;
    }

    private static byte[] ReadCategoriesBlob(string path, byte[] dek, int version)
    {
        using var database = VaultDatabase.OpenOrCreate(path);
        var stored = database.ReadSetting("categories");
        Assert.NotNull(stored);
        return AesGcmCipher.Decrypt(
            dek,
            stored!.Nonce,
            stored.Ciphertext,
            stored.Tag,
            Encoding.UTF8.GetBytes($"AegisVault|settings|categories|v{version}"));
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
