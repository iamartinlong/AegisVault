using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using Avalonia;

namespace AegisVault.App.ViewModels;

/// <summary>
/// Sidebar tree node for a user category (or the "uncategorized" pseudo entry).
/// <see cref="NavMenuNode"/> is the data object the inline NavMenu builds its
/// containers from; the container's header is set to this instance, so the row
/// template binds straight to <see cref="Item"/>.
/// </summary>
public sealed class CategoryNavNode : NavMenuNode
{
    public static readonly DirectProperty<CategoryNavNode, CategoryItem> ItemProperty =
        AvaloniaProperty.RegisterDirect<CategoryNavNode, CategoryItem>(
            nameof(Item),
            node => node.Item,
            (node, value) => node.Item = value);

    private CategoryItem _item;

    public CategoryNavNode(CategoryItem item)
    {
        _item = item;
        Header = item;
        ItemKey = EntityKey.Parse(item.Key);
    }

    /// <summary>
    /// Row data. Replaced in place when counts change so the tree keeps its
    /// expansion state instead of being rebuilt.
    /// </summary>
    public CategoryItem Item
    {
        get => _item;
        set => SetAndRaise(ItemProperty, ref _item, value);
    }

    /// <summary>
    /// True for a real category row; the "uncategorized" pseudo row and system
    /// rows report false.
    /// </summary>
    public bool IsUserCategory => Item.IsUserCategory && Item.CategoryId is not null;
}
