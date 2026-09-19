namespace AegisVault.Core.Models;

/// <summary>What happens to the children of a category that is being deleted.</summary>
public enum CategoryDeleteMode
{
    /// <summary>Child categories are lifted to the deleted category's parent (default).</summary>
    PromoteChildren,

    /// <summary>The deletion is refused while the category still has children.</summary>
    Deny,

    /// <summary>The whole branch below the category is deleted as well.</summary>
    Cascade,
}
