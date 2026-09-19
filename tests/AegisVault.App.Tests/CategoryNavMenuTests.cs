using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using Xunit;
using Button = Avalonia.Controls.Button;
using Control = Avalonia.Controls.Control;
using Window = Avalonia.Controls.Window;

namespace AegisVault.App.Tests;

/// <summary>
/// Interaction contract for rendering the category sidebar with an inline
/// <see cref="NavMenu"/>. These assertions pin down the spike findings:
/// 1. the app-owned row template makes custom row content clickable - AtomUI's
///    stock template marks its content presenter IsHitTestVisible=False, which
///    prunes the whole row content from hit testing (dot, count and the per-row
///    action menu would never receive a pointer);
/// 2. clicking the row expands the parent without selecting it - parents are
///    submenu headers, not selectable items;
/// 3. clicking a leaf selects it.
/// Motion is disabled so the assertions do not race the inline expand animation.
/// </summary>
public sealed class CategoryNavMenuTests
{
    private static NavMenuNode Node(string header, params NavMenuNode[] children)
    {
        var node = new NavMenuNode { Header = header, ItemKey = EntityKey.Parse(header) };
        foreach (var child in children)
        {
            node.Entries.Add(child);
        }

        return node;
    }

    private static (Window Window, NavMenu Menu) ShowMenu(params NavMenuNode[] nodes)
    {
        var menu = new NavMenu
        {
            Mode = NavMenuMode.Inline,
            Width = 240,
            IsMotionEnabled = false,
            ItemsSource = new ObservableCollection<NavMenuNode>(nodes),
        };
        var window = new Window
        {
            Width = 320,
            Height = 480,
            Content = menu,
        };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, menu);
    }

    private static Control Row(Visual root, object dataContext)
        => root.GetVisualDescendants()
            .OfType<Control>()
            .First(candidate => ReferenceEquals(candidate.DataContext, dataContext));

    private static Control Header(Control row)
        => row.GetType().Name == "InlineNavMenuItemHeader"
            ? row
            : row.GetVisualDescendants().OfType<Control>()
                .First(candidate => candidate.GetType().Name == "InlineNavMenuItemHeader");

    private static void Click(TopLevel window, Visual target, double? x = null)
    {
        var point = target.TranslatePoint(
            new Point(x ?? target.Bounds.Width / 2, target.Bounds.Height / 2),
            window) ?? throw new InvalidOperationException("The target is not connected to the window.");
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static FuncDataTemplate<NavMenuNode> ActionTemplate(System.Action onClick)
        => new((_, _) =>
        {
            var button = new Button { Content = "act", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            button.Click += (_, _) => onClick();
            return button;
        });

    [Fact]
    public Task RowContentReceivesClicksWithoutActivatingTheRow() => Headless.Run(() =>
    {
        var clicks = 0;
        var child = Node("Child");
        var parent = Node("Parent", child);
        parent.HeaderTemplate = ActionTemplate(() => clicks++);

        var (window, menu) = ShowMenu(parent);

        var button = menu.GetVisualDescendants().OfType<Button>()
            .First(candidate => ReferenceEquals(candidate.DataContext, parent));
        Assert.True(button.Bounds.Width > 0, "The row template did not receive a layout pass.");

        Click(window, button);

        Assert.Equal(1, clicks);
        Assert.Null(menu.SelectedItem);
        var childRow = menu.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, child));
        Assert.True(childRow is null || !childRow.IsEffectivelyVisible, "The action must not expand the row.");
    });

    [Fact]
    public Task RowClicksExpandTheParentWithoutSelectingIt() => Headless.Run(() =>
    {
        var child = Node("Child");
        var parent = Node("Parent", child);
        parent.HeaderTemplate = ActionTemplate(() => { });

        var (window, menu) = ShowMenu(parent);

        var header = Header(Row(menu, parent));
        Click(window, header, header.Bounds.Width - 4);

        Assert.True(Row(menu, child).IsEffectivelyVisible, "Clicking the row must expand the parent node.");
        Assert.Null(menu.SelectedItem);
    });

    [Fact]
    public Task LeafClicksSelectTheNode() => Headless.Run(() =>
    {
        var child = Node("Child");
        var parent = Node("Parent", child);

        var (window, menu) = ShowMenu(parent);

        var parentHeader = Header(Row(menu, parent));
        Click(window, parentHeader, parentHeader.Bounds.Width - 4);

        var childRow = Row(menu, child);
        Assert.True(childRow.IsEffectivelyVisible, "The parent row click must expand the node.");

        Click(window, childRow);

        Assert.Same(child, menu.SelectedItem);
    });
}
