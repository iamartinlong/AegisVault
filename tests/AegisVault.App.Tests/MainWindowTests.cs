using System.Linq;
using AegisVault.App.Views;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Xunit;
using AtomUIMenuItem = AtomUI.Desktop.Controls.MenuItem;

namespace AegisVault.App.Tests;

public sealed class MainWindowTests
{
    [Fact]
    public Task LockOverlayUnlockButtonRaisesUnlockRequested() => Headless.Run(() =>
    {
        var window = new MainWindow();
        var raised = 0;
        window.UnlockRequested += () => raised++;

        window.ShowLockOverlay();

        var button = window.FindControl<Button>("UnlockButton");
        Assert.NotNull(button);
        Assert.True(button!.IsVisible);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, raised);
    });

    [Fact]
    public Task UnlockRequestedSurvivesLockUnlockCycle() => Headless.Run(() =>
    {
        var window = new MainWindow();
        var raised = 0;
        window.UnlockRequested += () => raised++;

        // Lock, then hide the overlay as an unlock would, then lock again:
        // the overlay button must keep working after every cycle.
        window.ShowLockOverlay();
        window.HideLockOverlay();
        window.ShowLockOverlay();

        var button = window.FindControl<Button>("UnlockButton")!;
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(2, raised);
    });

    [Fact]
    public Task SearchBoxShowsMagnifierPrefixIcon() => Headless.Run(() =>
    {
        var window = new MainWindow();

        var search = window.FindControl<AtomUI.Desktop.Controls.LineEdit>("SearchBox");

        Assert.NotNull(search);
        Assert.NotNull(search!.InnerLeftContent);
    });

    [Fact]
    public Task SortButtonExposesRadioMenuWithBothModes() => Headless.Run(() =>
    {
        var window = new MainWindow();

        var sort = window.FindControl<AtomUI.Desktop.Controls.DropdownButton>("SortButton");
        Assert.NotNull(sort);
        Assert.False(sort!.IsArrowVisible);

        var items = sort.DropdownFlyout?.Items.OfType<AtomUIMenuItem>().ToList();
        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.All(items, item => Assert.Equal(MenuItemToggleType.Radio, item.ToggleType));
        Assert.All(items, item => Assert.Equal("SortMode", item.GroupName));
    });
}
