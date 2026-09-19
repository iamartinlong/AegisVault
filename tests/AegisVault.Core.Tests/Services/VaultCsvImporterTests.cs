using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class VaultCsvImporterTests
{
    private static (VaultService Vault, string Directory) CreateVault()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aegis-core-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var vault = VaultService.CreateNew(Path.Combine(directory, "vault.aegis"), "master password"u8);
        return (vault, directory);
    }

    private static void Cleanup(VaultService vault, string directory)
    {
        vault.Dispose();
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void ParseRejectsExportsAboveTheRowLimit()
    {
        var builder = new System.Text.StringBuilder("name,username,password\n");
        for (var i = 0; i <= VaultCsvImporter.MaxRows; i++)
        {
            builder.Append("entry,user,pw\n");
        }

        var exception = Assert.Throws<CsvImportLimitException>(() => VaultCsvImporter.Parse(builder.ToString()));
        Assert.Contains(VaultCsvImporter.MaxRows.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportsBitwardenExport()
    {
        const string csv = """
            folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp
            work,true,login,"GitHub, Inc.","some ""quoted"" note",,0,https://github.com,alice,"p@ss,word",JBSWY3DPEHPK3PXP
            ,false,login,Email,,,0,https://mail.example.com,bob,bobpass,
            """;
        var (vault, directory) = CreateVault();
        try
        {
            var result = VaultCsvImporter.Import(vault, csv);

            Assert.Equal(2, result.Imported);
            Assert.Equal(0, result.Skipped);

            var github = vault.Entries.Single(entry => entry.Title == "GitHub, Inc.");
            Assert.Equal("alice", github.Username);
            Assert.Equal("p@ss,word", github.Password);
            Assert.Equal("https://github.com", github.Url);
            Assert.Equal("some \"quoted\" note", github.Notes);
            Assert.Equal("JBSWY3DPEHPK3PXP", github.TotpSecret);
            Assert.Equal(["work"], github.Tags);
            Assert.True(github.IsFavorite);

            var email = vault.Entries.Single(entry => entry.Title == "Email");
            Assert.Equal("bob", email.Username);
            Assert.False(email.IsFavorite);
        }
        finally
        {
            Cleanup(vault, directory);
        }
    }

    [Fact]
    public void ImportsGenericCsv()
    {
        const string csv = "name,username,password,url,notes\r\nSite1,u1,p1,https://a.example.com,\r\nSite2,u2,p2,,n2\r\n";
        var (vault, directory) = CreateVault();
        try
        {
            var result = VaultCsvImporter.Import(vault, csv);

            Assert.Equal(2, result.Imported);
            var site1 = vault.Entries.Single(entry => entry.Title == "Site1");
            Assert.Equal("u1", site1.Username);
            Assert.Equal("p1", site1.Password);
            Assert.Equal("https://a.example.com", site1.Url);
        }
        finally
        {
            Cleanup(vault, directory);
        }
    }

    [Fact]
    public void SkipsRowsWithoutAnyContent()
    {
        const string csv = "name,username,password\n\n,\n,,\n,,";
        var (vault, directory) = CreateVault();
        try
        {
            var result = VaultCsvImporter.Import(vault, csv);

            Assert.Equal(0, result.Imported);
            Assert.Equal(4, result.Skipped);
        }
        finally
        {
            Cleanup(vault, directory);
        }
    }

    [Fact]
    public void EmptyCsvImportsNothing()
    {
        var (vault, directory) = CreateVault();
        try
        {
            var result = VaultCsvImporter.Import(vault, string.Empty);
            Assert.Equal(0, result.Imported);
        }
        finally
        {
            Cleanup(vault, directory);
        }
    }

    [Fact]
    public void ParseCsvHandlesQuotesEscapesAndCrlf()
    {
        var rows = VaultCsvImporter.ParseCsv("a,\"b\"\"c\",d\r\n\"x\r\ny\",,z\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b\"c", "d"], rows[0]);
        Assert.Equal(["x\r\ny", "", "z"], rows[1]);
    }

    [Fact]
    public void ParseDoesNotTouchTheVaultAndAcceptsBitwardenFavouriteFlags()
    {
        // Real Bitwarden exports write favourites as 1/0, not true/false.
        const string csv = """
            folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp
            work,1,login,One,,,,,,,
            ,0,login,Two,,,,,,,
            ,yes,login,Three,,,,,,,
            ,y,login,Four,,,,,,,
            """;

        var (entries, skipped) = VaultCsvImporter.Parse(csv);

        Assert.Equal(4, entries.Count);
        Assert.Equal(0, skipped);
        Assert.True(entries.Single(entry => entry.Title == "One").IsFavorite);
        Assert.False(entries.Single(entry => entry.Title == "Two").IsFavorite);
        Assert.True(entries.Single(entry => entry.Title == "Three").IsFavorite);
        Assert.True(entries.Single(entry => entry.Title == "Four").IsFavorite);
    }

    [Fact]
    public void BulkAddWritesEveryEntryInOneCall()
    {
        var (vault, directory) = CreateVault();
        try
        {
            var entries = Enumerable.Range(1, 50)
                .Select(index => new PasswordEntry { Title = $"Bulk {index:D2}", Username = $"u{index}" })
                .ToList();

            var added = vault.AddEntries(entries);

            Assert.Equal(50, added.Count);
            Assert.Equal(50, vault.Entries.Count);
            Assert.All(added, entry => Assert.NotEqual(Guid.Empty, entry.Id));
            Assert.All(added, entry => Assert.NotEqual(default, entry.CreatedAt));

            // Reopen from disk: the batch must have been committed.
            var path = vault.VaultPath;
            vault.Dispose();
            using var reopened = VaultService.Open(path);
            Assert.Equal(VaultUnlockStatus.Success, reopened.Unlock("master password"u8));
            Assert.Equal(50, reopened.Entries.Count);
            Assert.Contains(reopened.Entries, entry => entry.Title == "Bulk 50");
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
