namespace AegisVault.Core.Models;

/// <summary>
/// User-defined category (single assignment per entry), stored encrypted inside
/// the vault alongside the entries. Tags remain the multi-assignment dimension.
/// </summary>
public sealed record Category
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
