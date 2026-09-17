namespace AegisVault.Core.Models;

/// <summary>
/// User-defined category (single assignment per entry), stored encrypted inside
/// the vault alongside the entries. Tags remain the multi-assignment dimension.
/// </summary>
public sealed record Category
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Palette key ("red", "blue", …) or a literal "#RRGGBB" for custom colours;
    /// empty means "unset" and the UI falls back to a name-derived colour.
    /// </summary>
    public string Color { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
