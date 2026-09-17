using AegisVault.App.Services;
using AegisVault.App.ViewModels;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class VaultHealthViewModelTests : IDisposable
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

    public VaultHealthViewModelTests()
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

    private VaultService CreateVaultWithEntries()
    {
        var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.AddEntry(new PasswordEntry { Title = "GitHub", Username = "octocat", Password = "correct horse battery staple 42!" });
        vault.AddEntry(new PasswordEntry { Title = "WeakOne", Username = "a", Password = "123456" });
        vault.AddEntry(new PasswordEntry { Title = "WeakTwo", Username = "b", Password = "123456" });
        return vault;
    }

    [Fact]
    public Task ReportsSecurityIssuesAndWeakCategory() => Headless.RunAsync<object?>(async () =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        await viewModel.HealthAnalysis;

        Assert.False(viewModel.IsHealthAnalyzing);
        Assert.True(viewModel.HasSecurityIssues);
        Assert.Equal(2, viewModel.Health.WeakCount);
        Assert.Equal(2, viewModel.Health.ReusedCount);

        var weakCategory = viewModel.Categories.Single(category => category.Key == "weak");
        Assert.Equal(2, weakCategory.Count);

        viewModel.ShowSecurityCommand.Execute(null);
        Assert.Equal(weakCategory, viewModel.SelectedCategory);
        Assert.Equal(2, viewModel.FilteredEntries.Count);
        Assert.All(viewModel.FilteredEntries, entry => Assert.Contains(entry.Title, new[] { "WeakOne", "WeakTwo" }));
        return null;
    });

    [Fact]
    public Task HealthAnalysisShowsAPlaceholderUntilItLands() => Headless.RunAsync<object?>(async () =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        // The analysis is kicked off in the constructor: until its continuation
        // runs, the shell must show the loading state, not a wrong "all good".
        Assert.True(viewModel.IsHealthAnalyzing);
        Assert.Equal(Localization.Loc.T("Main_HealthAnalyzing"), viewModel.HealthSummary);
        Assert.Equal(Localization.Loc.T("Main_HealthAnalyzingCompact"), viewModel.HealthCompactText);
        Assert.False(viewModel.HasSecurityIssues);

        await viewModel.HealthAnalysis;

        Assert.False(viewModel.IsHealthAnalyzing);
        Assert.DoesNotContain(Localization.Loc.T("Main_HealthAnalyzing"), viewModel.HealthSummary);
        return null;
    });

    [Fact]
    public Task CleanVaultHidesWeakCategory() => Headless.RunAsync<object?>(async () =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.AddEntry(new PasswordEntry { Title = "Strong", Username = "a", Password = "correct horse battery staple 42!" });
        using var viewModel = new MainViewModel(vault);

        await viewModel.HealthAnalysis;

        Assert.False(viewModel.HasSecurityIssues);
        Assert.DoesNotContain(viewModel.Categories, category => category.Key == "weak");
        Assert.Contains("全部良好", viewModel.HealthSummary);
        return null;
    });

    [Fact]
    public Task StaleEntriesShowStaleCategoryAndSummary() => Headless.RunAsync<object?>(async () =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.AddEntry(new PasswordEntry { Title = "Old", Username = "a", Password = "correct horse battery staple 42!" });
        using var viewModel = new MainViewModel(vault, null, new FixedTimeProvider(DateTimeOffset.UtcNow.AddDays(400)));

        await viewModel.HealthAnalysis;

        Assert.Equal(1, viewModel.Health.OldCount);
        Assert.False(viewModel.HasSecurityIssues);

        var staleCategory = viewModel.Categories.Single(category => category.Key == "old");
        Assert.Equal(1, staleCategory.Count);

        viewModel.ShowSecurityCommand.Execute(null);
        Assert.Equal(staleCategory, viewModel.SelectedCategory);
        Assert.Equal("Old", viewModel.FilteredEntries.Single().Title);
        Assert.Contains("未更新", viewModel.HealthSummary);
        return null;
    });

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public Task SortsFavoritesFirstThenByTitle() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.AddEntry(new PasswordEntry { Title = "Charlie" });
        vault.AddEntry(new PasswordEntry { Title = "Alpha" });
        vault.AddEntry(new PasswordEntry { Title = "Beta", IsFavorite = true });
        using var viewModel = new MainViewModel(vault);

        Assert.Equal(
            ["Beta", "Alpha", "Charlie"],
            viewModel.FilteredEntries.Select(entry => entry.Title).ToList());
        
    });

    [Fact]
    public Task ReloadFromVaultPicksUpImportedEntries() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);
        Assert.Equal(3, viewModel.FilteredEntries.Count);

        var result = VaultCsvImporter.Import(vault, "name,username,password\nImported,u,p\n");
        Assert.Equal(1, result.Imported);

        viewModel.ReloadFromVault();
        Assert.Equal(4, viewModel.FilteredEntries.Count);
        Assert.Contains(viewModel.FilteredEntries, entry => entry.Title == "Imported");
        
    });
}
