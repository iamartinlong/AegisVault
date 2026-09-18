using AegisVault.App.Localization;
using AegisVault.App.Services;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AegisVault.App.ViewModels;

public partial class GeneratorViewModel : ObservableObject
{
    private readonly ClipboardService? _clipboard;

    [ObservableProperty]
    private double length = 20;

    [ObservableProperty]
    private bool includeLowercase = true;

    [ObservableProperty]
    private bool includeUppercase = true;

    [ObservableProperty]
    private bool includeDigits = true;

    [ObservableProperty]
    private bool includeSymbols = true;

    [ObservableProperty]
    private bool excludeAmbiguous;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private double entropyBits;

    [ObservableProperty]
    private string? copyStatus;

    [ObservableProperty]
    private double strengthPercent;

    [ObservableProperty]
    private string strengthSummary = string.Empty;

    public string LengthText => Loc.Format("Generator_LengthFormat", Length);

    public string EntropyText => StrengthFormatting.FormatInfo(EntropyBits);

    public GeneratorViewModel(ClipboardService? clipboard = null, PasswordGeneratorOptions? options = null)
    {
        _clipboard = clipboard;

        if (options is not null)
        {
            // Apply the saved defaults before the first generation so the
            // dialog opens with the user's configured options. Assign through
            // the properties so the bound controls see the values too.
            Length = options.Length;
            IncludeLowercase = options.IncludeLowercase;
            IncludeUppercase = options.IncludeUppercase;
            IncludeDigits = options.IncludeDigits;
            IncludeSymbols = options.IncludeSymbols;
            ExcludeAmbiguous = options.ExcludeAmbiguous;
        }

        Regenerate();
    }

    partial void OnLengthChanged(double value)
    {
        OnPropertyChanged(nameof(LengthText));
        Regenerate();
    }

    partial void OnEntropyBitsChanged(double value) => OnPropertyChanged(nameof(EntropyText));

    partial void OnIncludeLowercaseChanged(bool value) => Regenerate();

    partial void OnIncludeUppercaseChanged(bool value) => Regenerate();

    partial void OnIncludeDigitsChanged(bool value) => Regenerate();

    partial void OnIncludeSymbolsChanged(bool value) => Regenerate();

    partial void OnExcludeAmbiguousChanged(bool value) => Regenerate();

    [RelayCommand]
    private void Regenerate()
    {
        CopyStatus = null;

        var options = new PasswordGeneratorOptions
        {
            Length = (int)Math.Round(Length),
            IncludeLowercase = IncludeLowercase,
            IncludeUppercase = IncludeUppercase,
            IncludeDigits = IncludeDigits,
            IncludeSymbols = IncludeSymbols,
            ExcludeAmbiguous = ExcludeAmbiguous,
        };

        try
        {
            Password = PasswordGenerator.Generate(options);
            EntropyBits = PasswordGenerator.EstimateEntropyBits(options);

            var strength = PasswordStrengthEstimator.Evaluate(Password);
            StrengthPercent = strength.Score * 25;
            StrengthSummary = StrengthFormatting.FormatSummary(strength.Score, strength.Label, strength.CrackTime);
        }
        catch (ArgumentException)
        {
            Password = string.Empty;
            EntropyBits = 0;
            StrengthPercent = 0;
            StrengthSummary = string.Empty;
        }
    }

    [RelayCommand]
    private async Task CopyPasswordAsync()
    {
        if (_clipboard is null || string.IsNullOrEmpty(Password))
        {
            return;
        }

        await _clipboard.CopyAsync(Password);
        CopyStatus = Loc.T("Generator_Copied");
    }
}
