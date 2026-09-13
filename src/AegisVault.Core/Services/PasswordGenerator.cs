using System.Security.Cryptography;
using AegisVault.Core.Models;

namespace AegisVault.Core.Services;

/// <summary>Cryptographically secure password generator.</summary>
public static class PasswordGenerator
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.<>?";
    private const string Ambiguous = "Il1O0o";

    public static string Generate(PasswordGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Length <= 0)
        {
            throw new ArgumentException("Password length must be positive.", nameof(options));
        }

        var classes = BuildClasses(options);
        if (classes.Count == 0)
        {
            throw new ArgumentException("At least one character class must be selected.", nameof(options));
        }

        if (options.RequireEachSelectedClass && options.Length < classes.Count)
        {
            throw new ArgumentException("Password length is too short for the selected character classes.", nameof(options));
        }

        var pool = string.Concat(classes);
        var characters = new char[options.Length];
        var index = 0;

        if (options.RequireEachSelectedClass)
        {
            foreach (var characterClass in classes)
            {
                characters[index++] = characterClass[RandomNumberGenerator.GetInt32(characterClass.Length)];
            }
        }

        while (index < characters.Length)
        {
            characters[index++] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }

        Shuffle(characters);
        return new string(characters);
    }

    /// <summary>Estimated entropy in bits (length x log2(pool size)).</summary>
    public static double EstimateEntropyBits(PasswordGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var poolSize = 0;
        foreach (var characterClass in BuildClasses(options))
        {
            poolSize += characterClass.Length;
        }

        return poolSize == 0 || options.Length <= 0
            ? 0
            : options.Length * Math.Log2(poolSize);
    }

    private static List<string> BuildClasses(PasswordGeneratorOptions options)
    {
        var classes = new List<string>(4);

        if (options.IncludeLowercase)
        {
            classes.Add(Filter(Lowercase, options.ExcludeAmbiguous));
        }

        if (options.IncludeUppercase)
        {
            classes.Add(Filter(Uppercase, options.ExcludeAmbiguous));
        }

        if (options.IncludeDigits)
        {
            classes.Add(Filter(Digits, options.ExcludeAmbiguous));
        }

        if (options.IncludeSymbols)
        {
            classes.Add(Symbols);
        }

        return classes.Where(static c => c.Length > 0).ToList();
    }

    private static string Filter(string source, bool excludeAmbiguous)
        => excludeAmbiguous ? new string([.. source.Where(c => !Ambiguous.Contains(c))]) : source;

    private static void Shuffle(char[] characters)
    {
        for (var i = characters.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (characters[i], characters[j]) = (characters[j], characters[i]);
        }
    }
}
