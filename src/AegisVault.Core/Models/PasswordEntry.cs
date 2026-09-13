namespace AegisVault.Core.Models;

/// <summary>
/// Decrypted password entry model. Lives only in memory while a vault is unlocked.
/// </summary>
public sealed record PasswordEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string TotpSecret { get; init; } = string.Empty;

    public List<string> Tags { get; init; } = [];

    public Dictionary<string, string> CustomFields { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
