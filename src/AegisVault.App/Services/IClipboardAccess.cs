namespace AegisVault.App.Services;

/// <summary>Minimal clipboard abstraction so the service stays testable.</summary>
public interface IClipboardAccess
{
    Task SetTextAsync(string text);

    Task<string?> GetTextAsync();

    Task ClearAsync();
}
