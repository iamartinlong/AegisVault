using AegisVault.App.ViewModels;
using AegisVault.App.Views;
using Xunit;

namespace AegisVault.App.Tests;
public sealed class UnlockWindowTests
{
    [Fact]
    public Task CanBeShownWithViewModel() => Headless.Run(() =>
    {
        var viewModel = new UnlockViewModel();
        var window = new UnlockWindow { DataContext = viewModel };

        window.Show();

        Assert.True(window.IsVisible);
        window.Close();
    });

    [Fact]
    public Task CreateModeShowsCreatePanel() => Headless.Run(() =>
    {
        var viewModel = new UnlockViewModel { ModeIndex = UnlockViewModel.CreateMode };
        var window = new UnlockWindow { DataContext = viewModel };

        window.Show();

        Assert.True(viewModel.IsCreateMode);
        window.Close();
    });

    [Fact]
    public Task AcceptsVaultFileDrops() => Headless.Run(() =>
    {
        var viewModel = new UnlockViewModel();
        var window = new UnlockWindow { DataContext = viewModel };

        window.Show();

        // Drag & drop is wired through the DragDrop attached property
        // (Avalonia 12 moved it off InputElement).
        Assert.True(Avalonia.Input.DragDrop.GetAllowDrop(window));

        window.Close();
    });
}
