using AegisVault.App.Localization;
using AegisVault.App.ViewModels;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

/// <summary>
/// The startup (auto-start / start-minimized) switches persist through the
/// shell; the OS owns the auto-start entry, so a refused write must keep the
/// switch off and surface a message.
/// </summary>
public sealed class SettingsStartupTests : IDisposable
{
    private static readonly byte[] Password = "master password"u8.ToArray();

    private static readonly VaultOptions FastOptions = new()
    {
        Kdf = new KdfParameters
        {
            Algorithm = KdfParameters.AlgorithmArgon2id,
            Iterations = 3,
            MemoryBytes = 8L * 1024 * 1024,
        },
    };

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "aegis-ui-tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public Task StartupOptionsAreHiddenWithoutSupport() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(TempVaultPath(), Password, FastOptions);
        var viewModel = new SettingsViewModel(vault, new SecureConfigService(vault), null, null, "system");

        Assert.False(viewModel.StartupSupported);
        Assert.False(viewModel.AutoStart);
        Assert.False(viewModel.StartMinimized);
    });

    [Fact]
    public Task SavingReportsAutoStartAndMinimizePreferences() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(TempVaultPath(), Password, FastOptions);
        var appliedAutoStart = new List<bool>();
        var appliedMinimized = new List<bool>();
        var viewModel = new SettingsViewModel(
            vault,
            new SecureConfigService(vault),
            null,
            null,
            "system",
            startupSupported: true,
            autoStart: false,
            startMinimized: false,
            applyAutoStart: enabled =>
            {
                appliedAutoStart.Add(enabled);
                return true;
            },
            applyStartMinimized: minimized => appliedMinimized.Add(minimized));

        viewModel.AutoStart = true;
        viewModel.StartMinimized = true;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal([true], appliedAutoStart);
        Assert.Equal([true], appliedMinimized);
        Assert.Equal(Loc.T("Settings_StatusSaved"), viewModel.StatusMessage);
    });

    [Fact]
    public Task RefusedAutoStartKeepsTheSwitchOff() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(TempVaultPath(), Password, FastOptions);
        var appliedMinimized = new List<bool>();
        var viewModel = new SettingsViewModel(
            vault,
            new SecureConfigService(vault),
            null,
            null,
            "system",
            startupSupported: true,
            applyAutoStart: _ => false,
            applyStartMinimized: minimized => appliedMinimized.Add(minimized));

        viewModel.AutoStart = true;
        viewModel.StartMinimized = true;
        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.AutoStart);
        Assert.Equal(Loc.T("Settings_StatusAutoStartFailed"), viewModel.StatusMessage);
        Assert.Empty(appliedMinimized);
    });

    private string TempVaultPath()
        => Path.Combine(_directory, Guid.NewGuid().ToString("N"), "vault.aegis");
}
