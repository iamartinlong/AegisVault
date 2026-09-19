using System.Text;
using AegisVault.App.Localization;
using AegisVault.App.Services;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class CsvImportFlowTests : IDisposable
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

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "aegis-ui-tests", Guid.NewGuid().ToString("N"));

    public CsvImportFlowTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task ImportsSmallFiles()
    {
        using var vault = VaultService.CreateNew(Path.Combine(_directory, "vault.aegis"), Password, FastOptions);
        var path = Path.Combine(_directory, "import.csv");
        await File.WriteAllTextAsync(path, "name,username,password\nImported,user,pw\n");

        string? status = null;
        var result = await CsvImportFlow.RunAsync(vault, path, value => status = value);

        Assert.Equal(1, result.Imported);
        Assert.Equal(Loc.Format("Settings_StatusImportDone", 1, 0), status);
    }

    [Fact]
    public async Task RejectsFilesAboveTheSizeLimit()
    {
        using var vault = VaultService.CreateNew(Path.Combine(_directory, "vault.aegis"), Password, FastOptions);
        var path = Path.Combine(_directory, "huge.csv");
        using (var stream = File.Create(path))
        {
            stream.SetLength(CsvImportFlow.MaxImportBytes + 1);
        }

        string? status = null;
        var result = await CsvImportFlow.RunAsync(vault, path, value => status = value);

        Assert.Equal(0, result.Imported);
        Assert.Equal(Loc.T("Settings_StatusImportTooLarge"), status);
        Assert.Empty(vault.Entries);
    }

    [Fact]
    public async Task RejectsExportsAboveTheRowLimit()
    {
        using var vault = VaultService.CreateNew(Path.Combine(_directory, "vault.aegis"), Password, FastOptions);
        var path = Path.Combine(_directory, "rows.csv");
        var builder = new StringBuilder("name,username,password\n");
        for (var i = 0; i <= VaultCsvImporter.MaxRows; i++)
        {
            builder.Append("entry,user,pw\n");
        }

        await File.WriteAllTextAsync(path, builder.ToString());

        string? status = null;
        var result = await CsvImportFlow.RunAsync(vault, path, value => status = value);

        Assert.Equal(0, result.Imported);
        Assert.Equal(Loc.T("Settings_StatusImportTooLarge"), status);
        Assert.Empty(vault.Entries);
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
