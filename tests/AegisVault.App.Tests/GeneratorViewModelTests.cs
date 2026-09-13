using AegisVault.App.Services;
using AegisVault.App.ViewModels;
using AegisVault.Core.Models;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class GeneratorViewModelTests
{
    [Fact]
    public void GeneratesWithDefaults()
    {
        var viewModel = new GeneratorViewModel();

        Assert.Equal(20, viewModel.Password.Length);
        Assert.True(viewModel.EntropyBits > 100, $"Expected > 100 bits, got {viewModel.EntropyBits:F1}.");
    }

    [Fact]
    public void RegeneratesOnLengthChange()
    {
        var viewModel = new GeneratorViewModel { Length = 32 };

        Assert.Equal(32, viewModel.Password.Length);
    }

    [Fact]
    public void ExcludeAmbiguousFiltersCharacters()
    {
        var viewModel = new GeneratorViewModel { ExcludeAmbiguous = true };

        Assert.DoesNotContain(viewModel.Password, c => "Il1O0o".Contains(c));
    }

    [Fact]
    public void DisablingAllClassesYieldsEmptyPassword()
    {
        var viewModel = new GeneratorViewModel
        {
            IncludeLowercase = false,
            IncludeUppercase = false,
            IncludeDigits = false,
            IncludeSymbols = false,
        };

        Assert.Equal(string.Empty, viewModel.Password);
        Assert.Equal(0, viewModel.EntropyBits);
    }

    [Fact]
    public void RegenerateCommandProducesNewPassword()
    {
        var viewModel = new GeneratorViewModel();
        var first = viewModel.Password;

        viewModel.RegenerateCommand.Execute(null);

        Assert.NotEqual(first, viewModel.Password);
    }

    [Fact]
    public Task CopyPasswordUsesSessionClipboardService() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var clipboard = new ClipboardService(fake, () => new UserConfig());

        var viewModel = new GeneratorViewModel(clipboard);
        await viewModel.CopyPasswordCommand.ExecuteAsync(null);

        Assert.Equal(viewModel.Password, fake.Text);
        Assert.False(string.IsNullOrEmpty(viewModel.CopyStatus));
        return null;
    });
}
