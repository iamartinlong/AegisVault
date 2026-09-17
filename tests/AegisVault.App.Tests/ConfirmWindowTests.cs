using Avalonia.Controls;
using AegisVault.App.Views;
using Xunit;

namespace AegisVault.App.Tests;

/// <summary>
/// The confirmation dialog grew optional button labels so the language switch
/// can offer "restart now / later" instead of the generic OK/cancel.
/// </summary>
public sealed class ConfirmWindowTests
{
    [Fact]
    public Task CustomButtonLabelsAreApplied() => Headless.Run(() =>
    {
        var window = new ConfirmWindow("title", "message", "Restart now", "Later");
        try
        {
            var ok = window.FindControl<Button>("OkButton");
            var cancel = window.FindControl<Button>("CancelButton");

            Assert.NotNull(ok);
            Assert.NotNull(cancel);
            Assert.Equal("Restart now", ok!.Content);
            Assert.Equal("Later", cancel!.Content);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task DefaultsKeepTheLocalisedLabels() => Headless.Run(() =>
    {
        var window = new ConfirmWindow("title", "message");
        try
        {
            var ok = window.FindControl<Button>("OkButton");

            Assert.NotNull(ok);
            Assert.Equal(Localization.Loc.T("Common_Ok"), ok!.Content);
        }
        finally
        {
            window.Close();
        }
    });
}
