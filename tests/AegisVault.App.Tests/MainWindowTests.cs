using System.Linq;
using AegisVault.App.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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

    [Fact]
    public Task ThemeButtonExposesThreeModes() => Headless.Run(() =>
    {
        var window = new MainWindow();

        var theme = window.FindControl<AtomUI.Desktop.Controls.DropdownButton>("ThemeButton");
        Assert.NotNull(theme);
        Assert.False(theme!.IsArrowVisible);

        var items = ReadRadioItems(theme, "ThemeMode");
        Assert.Equal(3, items.Count);
    });

    [Fact]
    public Task LanguageButtonExposesThreeModes() => Headless.Run(() =>
    {
        var window = new MainWindow();

        var language = window.FindControl<AtomUI.Desktop.Controls.DropdownButton>("LanguageButton");
        Assert.NotNull(language);
        Assert.False(language!.IsArrowVisible);

        var items = ReadRadioItems(language, "LanguageMode");
        Assert.Equal(3, items.Count);
    });

    [Fact]
    public Task FilteringKeepsTheSelectionWhenTheEntryStillMatches() => Headless.Run(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "aegis-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var vault = Core.Services.VaultService.CreateNew(
                Path.Combine(directory, "vault.aegis"),
                "master password"u8.ToArray(),
                new Core.Services.VaultOptions
                {
                    Kdf = new Core.Models.KdfParameters
                    {
                        Algorithm = Core.Models.KdfParameters.AlgorithmArgon2id,
                        Iterations = 3,
                        MemoryBytes = 8L * 1024 * 1024,
                    },
                });
            vault.AddEntry(new Core.Models.PasswordEntry { Title = "GitHub", Username = "octocat" });
            vault.AddEntry(new Core.Models.PasswordEntry { Title = "Mail", Username = "me@example.com" });

            using var viewModel = new ViewModels.MainViewModel(vault);
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var list = window.GetVisualDescendants().OfType<ListBox>().First(candidate =>
                (candidate.GetValue(AutomationProperties.AutomationIdProperty) as string) == "EntriesList");
            list.SelectedIndex = 0;
            var selected = viewModel.SelectedEntry;
            Assert.NotNull(selected);

            // The filter repopulates the bound collection; the selection must
            // survive as long as the entry still matches.
            viewModel.SearchText = selected!.Title;

            Assert.Same(selected, viewModel.SelectedEntry);
            Assert.Equal(0, list.SelectedIndex);

            window.Close();
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
    });

    [Fact]
    public Task TotpSeedEditorIsMaskedUntilRevealed() => Headless.Run(() =>
    {
        var window = new MainWindow();

        var box = window.FindControl<AtomUI.Desktop.Controls.LineEdit>("TotpSecretBox");

        Assert.NotNull(box);
        Assert.NotEqual('\0', box!.PasswordChar);
        Assert.False(box.RevealPassword);
    });

    [Fact]
    public Task ClosingClearsACopiedSecret() => Headless.RunAsync<object?>(async () =>
    {
        var fake = new FakeClipboardAccess();
        using var clipboard = new Services.ClipboardService(fake, () => new Core.Models.UserConfig());
        var window = new MainWindow();
        window.HookClipboardCleanup(clipboard);
        window.Show();

        await clipboard.CopyAsync("s3cret");
        Assert.Equal("s3cret", fake.Text);

        // Closing fires while the window is still alive, so the clipboard is
        // still reachable and the secret is cleared before exit.
        window.Close();

        Assert.Null(fake.Text);
        return null;
    });

    [Fact]
    public Task CategoryTreeRendersRowsWithAnActionMenuAndExpands() => Headless.Run(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "aegis-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var vault = Core.Services.VaultService.CreateNew(
                Path.Combine(directory, "vault.aegis"),
                "master password"u8.ToArray(),
                new Core.Services.VaultOptions
                {
                    Kdf = new Core.Models.KdfParameters
                    {
                        Algorithm = Core.Models.KdfParameters.AlgorithmArgon2id,
                        Iterations = 3,
                        MemoryBytes = 8L * 1024 * 1024,
                    },
                });

            var root = vault.AddCategory("Work");
            var child = vault.AddCategory("Servers", parentId: root.Id);
            vault.AddEntry(new Core.Models.PasswordEntry { Title = "Host", CategoryId = child.Id });

            using var viewModel = new ViewModels.MainViewModel(vault);
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var tree = window.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.NavMenu>().FirstOrDefault(
                candidate => (candidate.GetValue(AutomationProperties.AutomationIdProperty) as string) == "CategoryTree");
            Assert.NotNull(tree);

            var actionButtons = window.GetVisualDescendants().OfType<Button>()
                .Where(button => (button.GetValue(AutomationProperties.AutomationIdProperty) as string) == "CategoryActionsButton")
                .ToList();
            var rootButton = Assert.Single(actionButtons);
            Assert.Equal("Work", ((ViewModels.CategoryNavNode)rootButton.DataContext!).Item.DisplayName);

            // Clicking the row (outside the action button) expands the branch and the
            // nested row brings its own action button.
            var header = rootButton.GetVisualAncestors().OfType<Control>()
                .First(candidate => candidate.GetType().Name == "InlineNavMenuItemHeader");
            var point = header.TranslatePoint(new Point(header.Bounds.Width - 4, header.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var nested = window.GetVisualDescendants().OfType<Button>()
                .Where(button => (button.GetValue(AutomationProperties.AutomationIdProperty) as string) == "CategoryActionsButton")
                .Select(button => ((ViewModels.CategoryNavNode)button.DataContext!).Item.DisplayName)
                .ToList();
            Assert.Contains("Servers", nested);

            window.Close();
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
    });

    [Fact]
    public Task CategoryRowsSpanTheSidebarAndKeepCountsAligned() => Headless.Run(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "aegis-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var vault = Core.Services.VaultService.CreateNew(
                Path.Combine(directory, "vault.aegis"),
                "master password"u8.ToArray(),
                new Core.Services.VaultOptions
                {
                    Kdf = new Core.Models.KdfParameters
                    {
                        Algorithm = Core.Models.KdfParameters.AlgorithmArgon2id,
                        Iterations = 3,
                        MemoryBytes = 8L * 1024 * 1024,
                    },
                });

            // A parent (with a child) next to a leaf, plus an uncategorized entry so
            // the pseudo row is present as well.
            var root = vault.AddCategory("Work");
            vault.AddCategory("Servers", parentId: root.Id);
            vault.AddCategory("Personal");
            vault.AddEntry(new Core.Models.PasswordEntry { Title = "Loose" });

            using var viewModel = new ViewModels.MainViewModel(vault);
            var window = new MainWindow { DataContext = viewModel };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var tree = window.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.NavMenu>().First(
                candidate => (candidate.GetValue(AutomationProperties.AutomationIdProperty) as string) == "CategoryTree");

            // Row backgrounds span the whole menu (the Inline theme left-aligns by default).
            var headers = tree.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.BaseNavMenuItemHeader>().ToList();
            Assert.Equal(4, headers.Count);
            var frames = headers
                .Select(header => header.GetVisualDescendants().OfType<Control>().First(candidate => candidate.Name == "Frame"))
                .ToList();
            Assert.All(frames, frame => Assert.Equal(tree.Bounds.Width, frame.Bounds.Width, 0.5));

            // The count sits at the same right edge on a parent row, a leaf row and
            // the uncategorized row: the reserved arrow column must not shift it.
            var countRightEdges = tree.GetVisualDescendants().OfType<Control>()
                .Where(candidate => (candidate.GetValue(AutomationProperties.AutomationIdProperty) as string) == "CategoryCountText")
                .Select(count => Math.Round(count.TranslatePoint(new Point(count.Bounds.Width, 0), tree)!.Value.X, 1))
                .Distinct()
                .ToList();
            Assert.Single(countRightEdges);

            // Nesting indents the row content by one step per level (AtomUI's Level
            // is zero based, so the top level stays flush with the other lists).
            double NameLeft(string displayName)
            {
                var name = tree.GetVisualDescendants().OfType<Control>()
                    .First(candidate =>
                        (candidate.GetValue(AutomationProperties.AutomationIdProperty) as string) == "CategoryNameText" &&
                        candidate.DataContext is ViewModels.CategoryNavNode node &&
                        node.Item.DisplayName == displayName);
                return Math.Round(name.TranslatePoint(new Point(0, 0), tree)!.Value.X, 1);
            }

            var topLevel = NameLeft("Work");
            Assert.Equal(topLevel + 16, NameLeft("Servers"));
            Assert.Equal(topLevel, NameLeft("Personal"));

            window.Close();
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
    });

    private static List<AtomUIMenuItem> ReadRadioItems(
        AtomUI.Desktop.Controls.DropdownButton button,
        string groupName)
    {
        var items = button.DropdownFlyout?.Items.OfType<AtomUIMenuItem>().ToList();
        Assert.NotNull(items);
        Assert.All(items!, item => Assert.Equal(MenuItemToggleType.Radio, item.ToggleType));
        Assert.All(items!, item => Assert.Equal(groupName, item.GroupName));
        return items!;
    }
}
