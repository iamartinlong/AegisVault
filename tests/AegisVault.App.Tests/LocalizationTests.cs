using AegisVault.App.Localization;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void TablesHaveIdenticalKeys()
    {
        var zh = Loc.Keys(Loc.Chinese).OrderBy(key => key, StringComparer.Ordinal);
        var en = Loc.Keys(Loc.English).OrderBy(key => key, StringComparer.Ordinal);

        Assert.Equal(zh, en);
    }

    [Fact]
    public void TablesHaveNoEmptyValues()
    {
        foreach (var language in new[] { Loc.Chinese, Loc.English })
        {
            foreach (var key in Loc.Keys(language))
            {
                Assert.False(string.IsNullOrWhiteSpace(Loc.Get(language, key)), $"{language}:{key} is empty");
            }
        }
    }

    [Fact]
    public void DefaultLanguageIsChinese() => Assert.Equal(Loc.Chinese, Loc.Language);

    [Theory]
    [InlineData("system", "en-US", "en")]
    [InlineData("system", "en", "en")]
    [InlineData("system", "zh-CN", "zh")]
    [InlineData("system", "fr-FR", "zh")]
    [InlineData("en", "zh-CN", "en")]
    [InlineData("zh", "en-US", "zh")]
    [InlineData("EN", "zh-CN", "en")]
    [InlineData(null, "en-GB", "en")]
    [InlineData("garbage", null, "zh")]
    public void ResolvesLanguage(string? preference, string? culture, string expected)
        => Assert.Equal(expected, Loc.Resolve(preference, culture));

    [Fact]
    public void GetReturnsLanguageSpecificText()
    {
        Assert.Equal("复制", Loc.Get(Loc.Chinese, "Common_Copy"));
        Assert.Equal("Copy", Loc.Get(Loc.English, "Common_Copy"));
    }

    [Fact]
    public void FormatSubstitutesArguments()
        => Assert.Equal("已复制到剪贴板，12 秒后自动清除。", Loc.Format("Main_ToastFormat", 12));

    [Fact]
    public void UnknownKeyFallsBackToKey()
        => Assert.Equal("No_Such_Key", Loc.T("No_Such_Key"));

    [Fact]
    public void EveryKeyIsUsedByAtLeastOneTable() => Assert.NotEmpty(Loc.Keys(Loc.Chinese));
}
