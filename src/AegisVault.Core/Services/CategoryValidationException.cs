namespace AegisVault.Core.Services;

/// <summary>
/// Language-neutral reason a category mutation was rejected. The UI maps this to
/// a localized message; the exception message itself stays English (Core never
/// owns user-facing text).
/// </summary>
public enum CategoryValidationError
{
    /// <summary>The name was empty or whitespace only.</summary>
    NameEmpty,

    /// <summary>A sibling under the same parent already uses that name.</summary>
    NameDuplicate,

    /// <summary>The requested parent category does not exist.</summary>
    ParentMissing,

    /// <summary>The target was the category itself or one of its descendants.</summary>
    SelfOrSubtree,

    /// <summary>The move would nest categories deeper than the supported cap.</summary>
    DepthExceeded,
}

/// <summary>
/// A category operation was rejected by an invariant (duplicate sibling name,
/// missing parent, cycle, depth cap). Derives from <see cref="ArgumentException"/>
/// so existing callers keep working; new callers should switch on
/// <see cref="Error"/> and localize.
/// </summary>
public sealed class CategoryValidationException(
    CategoryValidationError error,
    string message,
    string? paramName = null) : ArgumentException(message, paramName)
{
    public CategoryValidationError Error { get; } = error;
}
