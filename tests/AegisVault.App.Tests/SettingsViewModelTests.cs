using AegisVault.App.ViewModels;
using AegisVault.App.Views;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class SettingsViewModelTests : IDisposable
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

    private readonly string _directory;
    private readonly string _vaultPath;

    public SettingsViewModelTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "aegis-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _vaultPath = Path.Combine(_directory, "vault.aegis");
    }

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
    public Task LoadsCurrentValues() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var config = new SecureConfigService(vault);
        config.Save(new UserConfig
        {
            AutoLockMinutes = 15,
            LockOnMinimize = false,
            ClipboardClearSeconds = 60,
            Generator = new PasswordGeneratorOptions { Length = 32, IncludeSymbols = false },
        });

        var viewModel = new SettingsViewModel(vault, config, null, null, "dark");

        Assert.Equal(15, viewModel.SelectedAutoLock?.Minutes);
        Assert.False(viewModel.LockOnMinimize);
        Assert.Equal(60, viewModel.SelectedClipboard?.Seconds);
        Assert.Equal(32, viewModel.GeneratorLength);
        Assert.False(viewModel.GeneratorSymbols);
        Assert.Equal("dark", viewModel.SelectedTheme?.Value);
    });

    [Fact]
    public Task SavePersistsConfigurationAndTheme() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var config = new SecureConfigService(vault);
        string? appliedTheme = null;
        var viewModel = new SettingsViewModel(vault, config, null, theme => appliedTheme = theme, "system");

        viewModel.SelectedAutoLock = viewModel.AutoLockOptions.Single(option => option.Minutes == 30);
        viewModel.LockOnSuspend = false;
        viewModel.SelectedClipboard = viewModel.ClipboardOptions.Single(option => option.Seconds == 15);
        viewModel.SelectedTheme = viewModel.ThemeOptions.Single(option => option.Value == "light");
        viewModel.SaveCommand.Execute(null);

        var saved = config.Current;
        Assert.Equal(30, saved.AutoLockMinutes);
        Assert.False(saved.LockOnSuspend);
        Assert.Equal(15, saved.ClipboardClearSeconds);
        Assert.Equal("light", appliedTheme);
    });

    [Fact]
    public Task ChangeMasterPasswordAppliesToVault() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var config = new SecureConfigService(vault);
        var viewModel = new SettingsViewModel(vault, config, null, null, "system")
        {
            NewMasterPassword = "new master password",
            ConfirmMasterPassword = "new master password",
        };

        viewModel.ChangeMasterPasswordCommand.Execute(null);

        vault.Lock();
        Assert.Equal(VaultUnlockStatus.WrongPassword, vault.Unlock(Password));
        Assert.Equal(VaultUnlockStatus.Success, vault.Unlock("new master password"u8));
    });

    [Fact]
    public Task ChangeMasterPasswordRejectsMismatch() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var config = new SecureConfigService(vault);
        var viewModel = new SettingsViewModel(vault, config, null, null, "system")
        {
            NewMasterPassword = "new master password",
            ConfirmMasterPassword = "different",
        };

        viewModel.ChangeMasterPasswordCommand.Execute(null);

        Assert.Contains("不一致", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.NotNull(viewModel.NewMasterPassword);
    });

    [Fact]
    public Task DeviceKeyRememberAndForget() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var config = new SecureConfigService(vault);
        var protector = new FakeKeyProtector();
        var viewModel = new SettingsViewModel(vault, config, protector, null, "system");

        Assert.True(viewModel.DeviceKeySupported);
        Assert.False(viewModel.HasDeviceKey);

        viewModel.RememberDeviceCommand.Execute(null);
        Assert.True(viewModel.HasDeviceKey);

        viewModel.ForgetDeviceCommand.Execute(null);
        Assert.False(viewModel.HasDeviceKey);
    });

    [Fact]
    public Task SaveAppliesLanguageSelection() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var config = new SecureConfigService(vault);
        string? appliedLanguage = null;
        var viewModel = new SettingsViewModel(
            vault,
            config,
            null,
            null,
            "system",
            currentLanguage: "en",
            applyLanguage: value => appliedLanguage = value);

        Assert.Equal("en", viewModel.SelectedLanguage?.Value);

        viewModel.SelectedLanguage = viewModel.LanguageOptions.Single(option => option.Value == "zh");
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("zh", appliedLanguage);
    });

    [Fact]
    public Task SettingsWindowCanBeConstructed() => Headless.Run(() =>
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var config = new SecureConfigService(vault);
        var viewModel = new SettingsViewModel(vault, config, null, null, "system");
        var window = new SettingsWindow { DataContext = viewModel };

        Assert.Equal("设置", window.Title);
    });
}
