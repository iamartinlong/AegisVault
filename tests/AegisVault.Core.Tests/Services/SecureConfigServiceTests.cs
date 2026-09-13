using System.Security.Cryptography;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.Core.Tests.Services;

public sealed class SecureConfigServiceTests : IDisposable
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

    public SecureConfigServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "aegis-tests", Guid.NewGuid().ToString("N"));
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
    public void ReturnsDefaultsWhenNothingWasSaved()
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var service = new SecureConfigService(vault);

        var config = service.Current;

        Assert.Equal(new UserConfig(), config);
        Assert.Equal(5, config.AutoLockMinutes);
        Assert.Equal(30, config.ClipboardClearSeconds);
    }

    [Fact]
    public void SaveThenReloadReturnsPersistedValues()
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        var service = new SecureConfigService(vault);

        service.Save(new UserConfig
        {
            AutoLockMinutes = 7,
            ClipboardClearSeconds = 15,
            LockOnMinimize = true,
            Generator = new PasswordGeneratorOptions { Length = 42, IncludeSymbols = false },
        });

        var reloaded = new SecureConfigService(vault).Current;

        Assert.Equal(7, reloaded.AutoLockMinutes);
        Assert.Equal(15, reloaded.ClipboardClearSeconds);
        Assert.True(reloaded.LockOnMinimize);
        Assert.Equal(42, reloaded.Generator.Length);
        Assert.False(reloaded.Generator.IncludeSymbols);
    }

    [Fact]
    public void CorruptSettingFallsBackToDefaults()
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        vault.WriteSetting(
            "user-config",
            RandomNumberGenerator.GetBytes(12),
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(16));

        var config = new SecureConfigService(vault).Current;

        Assert.Equal(new UserConfig(), config);
    }

    [Fact]
    public void ConfigIsNotStoredInPlaintext()
    {
        using var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions);
        new SecureConfigService(vault).Save(new UserConfig { AutoLockMinutes = 47 });

        vault.SaveBackup();
        var bytes = File.ReadAllBytes(_vaultPath + ".bak");

        Assert.DoesNotContain("autoLockMinutes", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.OrdinalIgnoreCase);
    }
}
