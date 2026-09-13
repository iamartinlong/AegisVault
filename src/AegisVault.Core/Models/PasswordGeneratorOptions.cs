namespace AegisVault.Core.Models;

public sealed record PasswordGeneratorOptions
{
    public int Length { get; init; } = 20;

    public bool IncludeLowercase { get; init; } = true;

    public bool IncludeUppercase { get; init; } = true;

    public bool IncludeDigits { get; init; } = true;

    public bool IncludeSymbols { get; init; } = true;

    /// <summary>Excludes visually ambiguous characters such as I, l, 1, O and 0.</summary>
    public bool ExcludeAmbiguous { get; init; }

    /// <summary>Guarantees at least one character from every selected class.</summary>
    public bool RequireEachSelectedClass { get; init; } = true;
}
