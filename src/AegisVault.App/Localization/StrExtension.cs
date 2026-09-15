using Avalonia.Markup.Xaml;

namespace AegisVault.App.Localization;

/// <summary>
/// XAML markup extension: <c>{loc:Str Unlock_Unlock}</c> resolves the key with
/// <see cref="Loc.T"/> at load time.
/// </summary>
public sealed class StrExtension : MarkupExtension
{
    public StrExtension()
    {
    }

    public StrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
