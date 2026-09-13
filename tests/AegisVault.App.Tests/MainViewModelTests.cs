using AegisVault.App.ViewModels;
using AegisVault.App.Views;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class MainViewModelTests : IDisposable
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
    private readonly string _vaultPath;

    public MainViewModelTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "aegis-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _vaultPath = Path.Combine(_directory, "vault.aegis");
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
    public Task LoadsEntriesAndFiltersBySearch() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        Assert.Equal(2, viewModel.FilteredEntries.Count);

        viewModel.SearchText = "git";
        Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("GitHub", viewModel.FilteredEntries[0].Title);

        viewModel.SearchText = string.Empty;
        Assert.Equal(2, viewModel.FilteredEntries.Count);
    });

    [Fact]
    public Task SelectingEntryPopulatesEditorAndSaves() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        Assert.Equal("octocat", viewModel.EditUsername);

        viewModel.EditTitle = "GitHub Work";
        viewModel.SaveEntryCommand.Execute(null);

        Assert.Contains(vault.Entries, entry => entry.Title == "GitHub Work");
    });

    [Fact]
    public Task AddAndDeleteEntry() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.AddEntryCommand.Execute(null);
        Assert.Equal(3, viewModel.FilteredEntries.Count);
        Assert.NotNull(viewModel.SelectedEntry);

        viewModel.DeleteEntryCommand.Execute(null);
        Assert.Equal(2, viewModel.FilteredEntries.Count);
        Assert.Null(viewModel.SelectedEntry);
    });

    [Fact]
    public Task ComputesTotpForSelectedEntry() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = vault.Entries.Single(entry => entry.Title == "GitHub");
        viewModel.EditTotpSecret = "JBSWY3DPEHPK3PXP";

        Assert.True(viewModel.HasTotp);
        Assert.Equal(6, viewModel.TotpCode.Length);
        Assert.InRange(viewModel.TotpRemaining, 1, 30);
    });

    [Fact]
    public Task InvalidTotpSecretShowsPlaceholder() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = vault.Entries.First();
        viewModel.EditTotpSecret = "not valid base32!";

        Assert.Equal("无效密钥", viewModel.TotpCode);
    });

    [Fact]
    public Task IsTotpValidReflectsCode() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = vault.Entries.Single(entry => entry.Title == "GitHub");
        viewModel.EditTotpSecret = "JBSWY3DPEHPK3PXP";
        Assert.True(viewModel.IsTotpValid);

        viewModel.EditTotpSecret = "not valid base32!";
        Assert.False(viewModel.IsTotpValid);
    });

    [Fact]
    public Task MainWindowCanBeConstructedWithViewModel() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);
        var window = new MainWindow { DataContext = viewModel };

        Assert.Equal("AegisVault", window.Title);
    });

    private VaultService CreateVaultWithEntries()
    {
        var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.AddEntry(new PasswordEntry { Title = "GitHub", Username = "octocat", Password = "s3cret" });
        vault.AddEntry(new PasswordEntry { Title = "Mail", Username = "me@example.com" });
        return vault;
    }
}
