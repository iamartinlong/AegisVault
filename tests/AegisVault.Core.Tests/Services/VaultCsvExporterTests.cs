using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class VaultCsvExporterTests
{
    [Fact]
    public void RoundTripsEntriesThroughTheImporter()
    {
        var category = new Category { Name = "Work" };
        var entries = new List<PasswordEntry>
        {
            new()
            {
                Title = "GitHub, \"work\"",
                Username = "octocat",
                Password = "s3cret\r\nline",
                Url = "https://github.com",
                Urls = ["https://github.com", "https://gist.github.com"],
                Notes = "first\nsecond",
                TotpSecret = "JBSWY3DPEHPK3PXP",
                Phone = "13800000000",
                Email = "ops@example.com",
                AppId = "cli_app",
                Secret = "client-secret",
                ApiKey = "api-key",
                IsFavorite = true,
                CategoryId = category.Id,
            },
            // Recycled entries never leave the application.
            new() { Title = "Recycled", DeletedAt = DateTimeOffset.UtcNow },
        };

        var csv = VaultCsvExporter.Export(entries, [category]);
        var (parsed, skipped) = VaultCsvImporter.Parse(csv);

        Assert.Equal(0, skipped);
        var entry = Assert.Single(parsed);
        Assert.Equal("GitHub, \"work\"", entry.Title);
        Assert.Equal("octocat", entry.Username);
        Assert.Equal("s3cret\r\nline", entry.Password);
        Assert.Equal("first\nsecond", entry.Notes);
        Assert.Equal(["https://github.com", "https://gist.github.com"], entry.Urls);
        Assert.Equal("https://github.com", entry.Url);
        Assert.Equal("13800000000", entry.Phone);
        Assert.Equal("ops@example.com", entry.Email);
        Assert.Equal("cli_app", entry.AppId);
        Assert.Equal("client-secret", entry.Secret);
        Assert.Equal("api-key", entry.ApiKey);
        Assert.Equal("JBSWY3DPEHPK3PXP", entry.TotpSecret);
        Assert.True(entry.IsFavorite);

        // The Bitwarden layout has no category column, so the folder round-trips as a tag.
        Assert.Equal(["Work"], entry.Tags);
    }

    [Fact]
    public void EmptyVaultProducesOnlyTheHeader()
    {
        var csv = VaultCsvExporter.Export([], []);
        var rows = VaultCsvImporter.ParseCsv(csv);

        var header = Assert.Single(rows);
        Assert.Equal("folder", header[0]);
        Assert.Contains("login_password", header);
        Assert.Contains("api_key", header);
    }

    [Fact]
    public void PlainValuesAreNotQuoted()
    {
        var csv = VaultCsvExporter.Export(
            [new PasswordEntry { Title = "Simple", Username = "u", Password = "p" }],
            []);

        var rows = VaultCsvImporter.ParseCsv(csv);
        Assert.Equal(2, rows.Count);
        Assert.Contains(",Simple,", csv);
    }
}
