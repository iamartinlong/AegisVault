using AegisVault.App.Localization;
using AegisVault.App.Services;
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
    public Task EmptyVaultShowsWelcomeHeroInsteadOfHints() => Headless.Run(() =>
    {
        var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        using var viewModel = new MainViewModel(vault);

        Assert.True(viewModel.IsVaultEmpty);
        Assert.False(viewModel.ShowListEmptyHint);
        Assert.False(viewModel.ShowSelectEntryHint);

        viewModel.AddEntryCommand.Execute(null);

        Assert.False(viewModel.IsVaultEmpty);
        Assert.False(viewModel.ShowSelectEntryHint);

        viewModel.SelectedEntry = null;
        Assert.True(viewModel.ShowSelectEntryHint);
    });

    [Fact]
    public Task ListEmptyHintOnlyShowsForNonEmptyVaults() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        Assert.False(viewModel.ShowListEmptyHint);

        viewModel.SearchText = "no-such-entry";

        Assert.True(viewModel.ShowListEmptyHint);
        Assert.Empty(viewModel.FilteredEntries);

        viewModel.SearchText = string.Empty;

        Assert.False(viewModel.ShowListEmptyHint);
    });

    [Fact]
    public Task SavesExtraCredentialFields() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.EditPhone = "13800000000";
        viewModel.EditAppId = "cli_123";
        viewModel.EditSecret = "shh-value";
        viewModel.EditApiKey = "key-value";
        viewModel.SaveEntryCommand.Execute(null);

        var saved = vault.Entries.Single(entry => entry.Title == "GitHub");
        Assert.Equal("13800000000", saved.Phone);
        Assert.Equal("cli_123", saved.AppId);
        Assert.Equal("shh-value", saved.Secret);
        Assert.Equal("key-value", saved.ApiKey);
    });

    [Fact]
    public Task SearchMatchesPhoneAndAppIdButNeverSecrets() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.EditPhone = "13800000000";
        viewModel.EditAppId = "cli_123";
        viewModel.EditSecret = "super-secret-value";
        viewModel.EditApiKey = "api-key-value";
        viewModel.SaveEntryCommand.Execute(null);

        viewModel.SearchText = "13800000000";
        Assert.Single(viewModel.FilteredEntries);

        viewModel.SearchText = "cli_123";
        Assert.Single(viewModel.FilteredEntries);

        viewModel.SearchText = "super-secret-value";
        Assert.Empty(viewModel.FilteredEntries);

        viewModel.SearchText = "api-key-value";
        Assert.Empty(viewModel.FilteredEntries);
    });

    [Fact]
    public Task SecretAndApiKeyPreviewsStayMaskedUntilRevealed() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.EditSecret = "shh-value";
        viewModel.EditApiKey = "key-value";

        Assert.DoesNotContain("shh", viewModel.SecretPreview);
        Assert.DoesNotContain("key", viewModel.ApiKeyPreview);

        viewModel.ToggleSecretRevealCommand.Execute(null);
        viewModel.ToggleApiKeyRevealCommand.Execute(null);

        Assert.Equal("shh-value", viewModel.SecretPreview);
        Assert.Equal("key-value", viewModel.ApiKeyPreview);

        viewModel.SelectedEntry = null;

        Assert.All(viewModel.SecretPreview, character => Assert.Equal('●', character));
        Assert.All(viewModel.ApiKeyPreview, character => Assert.Equal('●', character));
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

    [Fact]
    public Task CategoriesIncludeFavoritesAndTags() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        Assert.Contains(viewModel.Categories, category => category.Key == "all" && category.Count == 2);
        Assert.Contains(viewModel.Categories, category => category.IsFavorites && category.Count == 0);
        Assert.Contains(viewModel.Categories, category => category.Tag == "work" && category.Count == 1);
    });

    [Fact]
    public Task ToggleFavoriteUpdatesEntryAndFilter() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = vault.Entries.Single(entry => entry.Title == "GitHub");
        viewModel.ToggleFavoriteCommand.Execute(null);

        Assert.True(vault.Entries.Single(entry => entry.Title == "GitHub").IsFavorite);
        Assert.Contains(viewModel.Categories, category => category.IsFavorites && category.Count == 1);

        viewModel.SelectedCategory = viewModel.Categories.Single(category => category.IsFavorites);
        Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("GitHub", viewModel.FilteredEntries[0].Title);
    });

    [Fact]
    public Task BeginEditAndCancelRestoresFields() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = vault.Entries.Single(entry => entry.Title == "GitHub");
        viewModel.BeginEditCommand.Execute(null);
        Assert.True(viewModel.IsEditing);

        viewModel.EditTitle = "changed";
        viewModel.CancelEditCommand.Execute(null);

        Assert.False(viewModel.IsEditing);
        Assert.Equal("GitHub", viewModel.EditTitle);
    });

    [Fact]
    public Task CopyPasswordUsesClipboardService() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var clipboard = new ClipboardService(fake, () => new UserConfig());

        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault, clipboard);

        viewModel.SelectedEntry = vault.Entries.Single(entry => entry.Title == "GitHub");
        await viewModel.CopyPasswordCommand.ExecuteAsync(null);

        Assert.Equal("s3cret", fake.Text);
        return null;
    });

    [Fact]
    public Task ReportsPasswordStrengthForEditedEntry() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = vault.Entries.Single(entry => entry.Title == "GitHub");

        Assert.True(viewModel.EditPasswordStrengthPercent > 0);
        Assert.False(string.IsNullOrEmpty(viewModel.EditPasswordStrengthSummary));
    });

    [Fact]
    public Task CategoryCreateAssignFilterRenameDelete() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        Assert.True(viewModel.TryCreateCategory("Work", out var error));
        Assert.Null(error);

        var created = viewModel.Categories.Single(category => category.DisplayName == "Work");
        Assert.True(created.IsUserCategory);
        Assert.Equal(0, created.Count);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.BeginEditCommand.Execute(null);
        viewModel.SelectedCategoryChoice = viewModel.CategoryChoices.Single(choice => choice.Name == "Work");
        viewModel.SaveEntryCommand.Execute(null);

        Assert.Equal(created.CategoryId, vault.Entries.Single(entry => entry.Title == "GitHub").CategoryId);
        var refreshed = viewModel.Categories.Single(category => category.DisplayName == "Work");
        Assert.Equal(1, refreshed.Count);

        viewModel.SelectedCategory = refreshed;
        Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("GitHub", viewModel.FilteredEntries[0].Title);

        Assert.True(viewModel.TryRenameCategory(refreshed.CategoryId!.Value, "Job", out var renameError));
        Assert.Null(renameError);
        Assert.Contains(viewModel.Categories, category => category.DisplayName == "Job");

        Assert.False(viewModel.TryCreateCategory("job", out var duplicateError));
        Assert.NotNull(duplicateError);
        Assert.False(viewModel.TryCreateCategory("   ", out var emptyError));
        Assert.NotNull(emptyError);

        viewModel.DeleteCategory(refreshed.CategoryId!.Value);
        Assert.DoesNotContain(viewModel.Categories, category => category.CategoryId == refreshed.CategoryId);
        Assert.Null(vault.Entries.Single(entry => entry.Title == "GitHub").CategoryId);
    });

    [Fact]
    public Task NewEntryInheritsSelectedCategory() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.TryCreateCategory("Work", out _);
        viewModel.SelectedCategory = viewModel.Categories.Single(category => category.DisplayName == "Work");

        viewModel.AddEntryCommand.Execute(null);

        Assert.Equal(
            viewModel.SelectedCategory!.CategoryId,
            vault.Entries.Single(entry => entry.Title == "新条目").CategoryId);
    });

    [Fact]
    public Task SortModesAndViewChipsWork() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        Assert.True(viewModel.IsSortByName);
        Assert.Equal(["GitHub", "Mail"], viewModel.FilteredEntries.Select(entry => entry.Title).ToList());

        var mail = vault.Entries.Single(entry => entry.Title == "Mail");
        vault.UpdateEntry(mail);
        viewModel.ReloadFromVault();

        viewModel.SortByRecentCommand.Execute(null);
        Assert.True(viewModel.IsSortByRecent);
        Assert.Equal(["Mail", "GitHub"], viewModel.FilteredEntries.Select(entry => entry.Title).ToList());

        viewModel.SelectViewCommand.Execute("favorites");
        Assert.True(viewModel.IsViewFavorites);
        Assert.False(viewModel.IsViewAll);
        Assert.Contains("共 0 条", viewModel.FilteredCountText);
    });

    [Fact]
    public Task SaveRequiresTitle() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.BeginEditCommand.Execute(null);
        viewModel.EditTitle = "   ";
        viewModel.SaveEntryCommand.Execute(null);

        Assert.NotEmpty(viewModel.TitleError);
        Assert.True(viewModel.IsEditing);
        Assert.Contains(vault.Entries, entry => entry.Title == "GitHub");

        viewModel.EditTitle = "GitHub Work";
        viewModel.SaveEntryCommand.Execute(null);

        Assert.Empty(viewModel.TitleError);
        Assert.False(viewModel.IsEditing);
        Assert.Contains(vault.Entries, entry => entry.Title == "GitHub Work");
    });

    [Fact]
    public Task SaveNormalizesAndValidatesUrl() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.BeginEditCommand.Execute(null);
        viewModel.EditUrls = "example.com";
        viewModel.SaveEntryCommand.Execute(null);

        Assert.Empty(viewModel.UrlError);
        Assert.Equal("https://example.com", vault.Entries.Single(entry => entry.Title == "GitHub").Url);

        viewModel.BeginEditCommand.Execute(null);
        viewModel.EditUrls = "ftp://example.com";
        viewModel.SaveEntryCommand.Execute(null);

        Assert.NotEmpty(viewModel.UrlError);
        Assert.True(viewModel.IsEditing);
        Assert.Equal("https://example.com", vault.Entries.Single(entry => entry.Title == "GitHub").Url);

        viewModel.EditUrls = string.Empty;
        viewModel.SaveEntryCommand.Execute(null);

        Assert.Empty(viewModel.UrlError);
        Assert.False(viewModel.IsEditing);
        Assert.Equal(string.Empty, vault.Entries.Single(entry => entry.Title == "GitHub").Url);
    });

    [Fact]
    public Task SaveReportsTitleAndUrlErrorsTogether() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.BeginEditCommand.Execute(null);
        viewModel.EditTitle = "   ";
        viewModel.EditUrls = "ftp://example.com";
        viewModel.SaveEntryCommand.Execute(null);

        Assert.Equal(Loc.T("Main_TitleRequired"), viewModel.TitleError);
        Assert.Equal(Loc.Format("Main_UrlInvalidLine", 1), viewModel.UrlError);
        Assert.True(viewModel.IsEditing);

        viewModel.EditTitle = "GitHub";
        Assert.Empty(viewModel.TitleError);
        viewModel.EditUrls = "github.com";
        Assert.Empty(viewModel.UrlError);
    });

    [Fact]
    public Task SaveStoresMultipleUrls() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.BeginEditCommand.Execute(null);
        viewModel.EditUrls = "github.com\n  https://gist.github.com  \n\ngithub.com";
        viewModel.SaveEntryCommand.Execute(null);

        var saved = vault.Entries.Single(entry => entry.Title == "GitHub");
        Assert.Equal(["https://github.com", "https://gist.github.com"], saved.Urls);
        Assert.Equal("https://github.com", saved.Url);
        Assert.Equal(["https://github.com", "https://gist.github.com"], viewModel.UrlItems);
        Assert.Equal("https://github.com\nhttps://gist.github.com", viewModel.EditUrls);
    });

    [Fact]
    public Task SaveRejectsInvalidSecondUrl() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.BeginEditCommand.Execute(null);
        viewModel.EditUrls = "github.com\nnot-a-url";
        viewModel.SaveEntryCommand.Execute(null);

        Assert.Equal(Loc.Format("Main_UrlInvalidLine", 2), viewModel.UrlError);
        Assert.True(viewModel.IsEditing);
        Assert.Empty(vault.Entries.Single(entry => entry.Title == "GitHub").Urls);
    });

    [Fact]
    public Task LegacyUrlFillsUrlItemsAndSearchMatchesAllUrls() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        var github = vault.Entries.Single(entry => entry.Title == "GitHub");
        vault.UpdateEntry(github with { Url = "https://github.com", Urls = ["https://github.com", "https://gist.github.com"] });

        using var viewModel = new MainViewModel(vault);
        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");

        Assert.Equal(["https://github.com", "https://gist.github.com"], viewModel.UrlItems);
        Assert.Equal("https://github.com\nhttps://gist.github.com", viewModel.EditUrls);

        viewModel.SearchText = "gist";
        Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("GitHub", viewModel.FilteredEntries[0].Title);
    });

    [Fact]
    public Task SelectingEntryWithNullUrlsStillLoadsDetail() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        var github = vault.Entries.Single(entry => entry.Title == "GitHub");
        vault.UpdateEntry(github with { Url = "https://github.com", Urls = null! });

        using var viewModel = new MainViewModel(vault);
        viewModel.SelectedEntry = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");

        Assert.True(viewModel.HasSelection);
        Assert.Equal("GitHub", viewModel.EditTitle);
        Assert.Equal(["https://github.com"], viewModel.UrlItems);
        Assert.Equal("https://github.com", viewModel.EditUrls);
    });

    [Fact]
    public Task SelectionShowsCreationTime() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        using var viewModel = new MainViewModel(vault);

        var github = viewModel.FilteredEntries.Single(entry => entry.Title == "GitHub");
        viewModel.SelectedEntry = github;

        Assert.Equal(github.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), viewModel.CreatedAtDisplay);

        viewModel.SelectedEntry = null;
        Assert.Empty(viewModel.CreatedAtDisplay);
    });

    [Fact]
    public Task OpenUrlNormalizesAndReportsFailures() => Headless.Run(() =>
    {
        using var vault = CreateVaultWithEntries();
        var launcher = new RecordingUrlLauncher();
        using var viewModel = new MainViewModel(vault, urlLauncher: launcher);

        viewModel.OpenUrlCommand.Execute("github.com");
        Assert.Equal(["https://github.com"], launcher.Opened);

        viewModel.OpenUrlCommand.Execute("javascript:alert(1)");
        Assert.Equal(Loc.T("Main_UrlInvalid"), viewModel.StatusMessage);

        viewModel.OpenUrlCommand.Execute(string.Empty);
        Assert.Equal(Loc.T("Main_StatusUrlEmpty"), viewModel.StatusMessage);

        launcher.Result = false;
        viewModel.OpenUrlCommand.Execute("https://example.com");
        Assert.Equal(Loc.T("Main_StatusUrlOpenFailed"), viewModel.StatusMessage);
    });

    private VaultService CreateVaultWithEntries()
    {
        var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.AddEntry(new PasswordEntry
        {
            Title = "GitHub",
            Username = "octocat",
            Password = "s3cret",
            Tags = ["work", "mail"],
        });
        vault.AddEntry(new PasswordEntry { Title = "Mail", Username = "me@example.com" });
        return vault;
    }

    private sealed class RecordingUrlLauncher : IUrlLauncher
    {
        public List<string?> Opened { get; } = [];

        public bool Result { get; set; } = true;

        public bool IsSupported(string? url) => !string.IsNullOrEmpty(url);

        public bool TryOpen(string? url)
        {
            Opened.Add(url);
            return Result;
        }
    }
}
