using System.Reflection;
using System.Text.Json;
using AegisVault.Core.Models;
using AegisVault.Core.Storage;
using Xunit;

namespace AegisVault.Core.Tests.Storage;

public sealed class ModelMigrationsTests
{
    [Fact]
    public void MinimalLegacyEntryPayloadDeserializesWithNullCollections()
    {
        // Characterization of the source-generated context: missing collection
        // properties come back as null (unlike the reflection serializer).
        var entry = JsonSerializer.Deserialize(
            """{"Title":"Legacy"}""",
            VaultJsonContext.Default.PasswordEntry);

        Assert.NotNull(entry);
        Assert.Null(entry!.Tags);
        Assert.Null(entry.Urls);
        Assert.Null(entry.CustomFields);
    }

    [Fact]
    public void EntryToStringDoesNotLeakSecrets()
    {
        var entry = new PasswordEntry
        {
            Title = "GitHub",
            Username = "octocat",
            Password = "s3cret-value",
            TotpSecret = "JBSWY3DPEHPK3PXP",
        };

        var text = entry.ToString();

        Assert.Contains("GitHub", text);
        Assert.DoesNotContain("s3cret-value", text);
        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", text);
        Assert.DoesNotContain("octocat", text);
    }

    [Fact]
    public void UpgradeFromV1FillsEveryCollectionAndKeepsData()
    {
        var legacy = JsonSerializer.Deserialize(
            """{"Title":"Legacy","Url":"https://example.com","Tags":["work"]}""",
            VaultJsonContext.Default.PasswordEntry)!;

        var upgraded = ModelMigrations.UpgradeEntry(legacy, 1);

        Assert.Equal("Legacy", upgraded.Title);
        Assert.Equal("https://example.com", upgraded.Url);
        Assert.Equal(["work"], upgraded.Tags);
        Assert.Empty(upgraded.Urls);
        Assert.Empty(upgraded.CustomFields);
    }

    [Fact]
    public void UpgradeFillsEveryCollectionPropertyDeclaredOnTheModel()
    {
        // Guards future model changes: any new collection/object member must be
        // covered by ModelMigrations.Normalize, otherwise this test fails.
        var minimal = JsonSerializer.Deserialize(
            """{"Title":"Minimal"}""",
            VaultJsonContext.Default.PasswordEntry)!;

        var upgraded = ModelMigrations.UpgradeEntry(minimal, 1);

        foreach (var property in typeof(PasswordEntry).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var type = property.PropertyType;
            var isCollection = type.IsArray ||
                (type.IsGenericType &&
                 type.GetGenericTypeDefinition() is { } definition &&
                 (definition == typeof(List<>) ||
                  definition == typeof(Dictionary<,>) ||
                  definition == typeof(IReadOnlyList<>) ||
                  definition == typeof(IEnumerable<>)));

            if (isCollection)
            {
                Assert.True(
                    property.GetValue(upgraded) is not null,
                    $"PasswordEntry.{property.Name} is null after upgrade; extend ModelMigrations.Normalize.");
            }
        }
    }

    [Fact]
    public void UpgradeRejectsVersionZero()
    {
        var entry = new PasswordEntry { Title = "x" };
        Assert.Throws<InvalidDataException>(() => ModelMigrations.UpgradeEntry(entry, 0));
    }

    [Fact]
    public void UserConfigNormalizationRestoresMissingGenerator()
    {
        var config = JsonSerializer.Deserialize("{}", VaultJsonContext.Default.UserConfig)!;
        Assert.Null(config.Generator);

        var normalized = ModelMigrations.Normalize(config);

        Assert.NotNull(normalized.Generator);
        Assert.Equal(20, normalized.Generator.Length);
    }

    [Fact]
    public void CategoryNormalizationRestoresMissingName()
    {
        var categories = JsonSerializer.Deserialize(
            """[{"Id":"6f9619ff-8b86-d011-b42d-00cf4fc964ff"}]""",
            VaultJsonContext.Default.ListCategory)!;
        var category = Assert.Single(categories);
        Assert.Null(category.Name);

        var normalized = ModelMigrations.Normalize(category);

        Assert.Equal(string.Empty, normalized.Name);
        Assert.Equal(Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff"), normalized.Id);
    }
}
