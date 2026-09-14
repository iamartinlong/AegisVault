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
}
