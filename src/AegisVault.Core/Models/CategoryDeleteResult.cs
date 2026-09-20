namespace AegisVault.Core.Models;

/// <summary>A child category that had to be renamed while it was promoted.</summary>
/// <param name="Id">The category that was renamed.</param>
/// <param name="From">The name it had before the rename.</param>
/// <param name="To">The unique name it got.</param>
public sealed record CategoryRename(Guid Id, string From, string To);

/// <summary>Outcome of deleting a category, including any promotions that collided.</summary>
/// <param name="Removed">False when the category no longer existed.</param>
/// <param name="Renamed">
/// Children whose names clashed with an existing sibling at the destination and
/// were therefore renamed (for example <c>Work</c> becoming <c>Work (2)</c>).
/// </param>
public sealed record CategoryDeleteResult(bool Removed, IReadOnlyList<CategoryRename> Renamed);
