using AegisVault.App.Services;
using AegisVault.App.ViewModels;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class QuickAccessViewModelTests : IDisposable
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

    public QuickAccessViewModelTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "aegis-ui-tests", Guid.NewGuid().ToString("N"));
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

    private (VaultService Vault, MainViewModel Main) CreateMain(params PasswordEntry[] entries)
    {
        var vault = VaultService.CreateNew(Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".aegis"), Password, FastOptions);
        foreach (var entry in entries)
        {
            vault.AddEntry(entry);
        }

        return (vault, new MainViewModel(vault));
    }

    [Fact]
    public void FiltersByTitleUsernameUrlAndTags()
    {
        var (vault, main) = CreateMain(
            new PasswordEntry { Title = "GitHub", Username = "alice", Url = "https://github.com" },
            new PasswordEntry { Title = "GitLab", Username = "bob", Tags = ["work"] },
            new PasswordEntry { Title = "Email", Username = "carol" });
        using var _ = vault;
        using var __ = main;
        var viewModel = new QuickAccessViewModel(main);

        Assert.Equal(3, viewModel.FilteredEntries.Count);

        viewModel.SearchText = "git";
        Assert.Equal(2, viewModel.FilteredEntries.Count);

        viewModel.SearchText = "work";
        Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("GitLab", viewModel.FilteredEntries[0].Title);

        viewModel.SearchText = "alice";
        Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("GitHub", viewModel.FilteredEntries[0].Title);

        viewModel.SearchText = "zzz";
        Assert.Empty(viewModel.FilteredEntries);
        Assert.False(viewModel.HasResults);
    }

    [Fact]
    public void ActivateSelectsEntryInMainAndRaisesHide()
    {
        var (vault, main) = CreateMain(
            new PasswordEntry { Title = "GitHub" },
            new PasswordEntry { Title = "Email" });
        using var _ = vault;
        using var __ = main;
        var viewModel = new QuickAccessViewModel(main);

        var hidden = false;
        viewModel.HideRequested += () => hidden = true;

        viewModel.SearchText = "email";
        viewModel.SelectedEntry = viewModel.FilteredEntries[0];
        viewModel.ActivateCommand.Execute(null);

        Assert.True(hidden);
        Assert.Equal("Email", main.SelectedEntry?.Title);
    }

    [Fact]
    public void MoveSelectionClampsToRange()
    {
        var (vault, main) = CreateMain(
            new PasswordEntry { Title = "A" },
            new PasswordEntry { Title = "B" });
        using var _ = vault;
        using var __ = main;
        var viewModel = new QuickAccessViewModel(main);

        viewModel.MoveSelection(1);
        Assert.Equal("B", viewModel.SelectedEntry?.Title);

        viewModel.MoveSelection(10);
        Assert.Equal("B", viewModel.SelectedEntry?.Title);

        viewModel.MoveSelection(-10);
        Assert.Equal("A", viewModel.SelectedEntry?.Title);
    }

    [Fact]
    public async Task CopyPasswordWritesClipboard()
    {
        var (vault, main) = CreateMain(new PasswordEntry { Title = "GitHub", Password = "s3cret" });
        var fake = new FakeClipboardAccess();
        using var service = new ClipboardService(fake, () => new UserConfig { ClipboardClearSeconds = 30 });
        using var __ = main;
        try
        {
            var viewModel = new QuickAccessViewModel(main, service);

            await viewModel.CopyPasswordCommand.ExecuteAsync(viewModel.FilteredEntries[0]);
            Assert.Equal("s3cret", fake.Text);
        }
        finally
        {
            vault.Dispose();
        }
    }
}
