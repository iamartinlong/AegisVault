using AegisVault.App.ViewModels;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class UnlockViewModelTests : IDisposable
{
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

    public UnlockViewModelTests()
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
    public Task CreateThenUnlockFlow() => Headless.RunAsync<object?>(async () =>
    {
        var createModel = new UnlockViewModel
        {
            NewVaultPath = _vaultPath,
            ModeIndex = UnlockViewModel.CreateMode,
            MasterPassword = "master password",
            ConfirmPassword = "master password",
        };

        VaultService? created = null;
        createModel.VaultOpened += vault => created = vault;

        await createModel.CreateCommand.ExecuteAsync(null);

        Assert.Null(createModel.ErrorMessage);
        Assert.NotNull(created);
        created!.Dispose();

        var unlockModel = new UnlockViewModel
        {
            VaultPath = _vaultPath,
            MasterPassword = "master password",
        };

        VaultService? unlocked = null;
        unlockModel.VaultOpened += vault => unlocked = vault;

        await unlockModel.UnlockCommand.ExecuteAsync(null);

        Assert.Null(unlockModel.ErrorMessage);
        Assert.NotNull(unlocked);
        Assert.True(unlocked!.IsUnlocked);
        unlocked.Dispose();
        return null;
    });

    [Fact]
    public Task WrongPasswordShowsFriendlyError() => Headless.RunAsync<object?>(async () =>
    {
        using (VaultService.CreateNew(_vaultPath, "master password"u8, FastOptions))
        {
        }

        var model = new UnlockViewModel
        {
            VaultPath = _vaultPath,
            MasterPassword = "wrong password",
        };

        await model.UnlockCommand.ExecuteAsync(null);

        Assert.Equal("主密码错误。", model.ErrorMessage);
        return null;
    });

    [Fact]
    public Task MissingFileShowsFriendlyError() => Headless.RunAsync<object?>(async () =>
    {
        var model = new UnlockViewModel
        {
            VaultPath = Path.Combine(_directory, "missing.aegis"),
            MasterPassword = "whatever",
        };

        await model.UnlockCommand.ExecuteAsync(null);

        Assert.Equal("找不到密码库文件，请检查路径，或切换到“创建新密码库”。", model.ErrorMessage);
        return null;
    });

    [Fact]
    public Task MismatchedConfirmationShowsError() => Headless.RunAsync<object?>(async () =>
    {
        var model = new UnlockViewModel
        {
            NewVaultPath = _vaultPath,
            ModeIndex = UnlockViewModel.CreateMode,
            MasterPassword = "master password",
            ConfirmPassword = "different",
        };

        await model.CreateCommand.ExecuteAsync(null);

        Assert.Equal("两次输入的密码不一致。", model.ErrorMessage);
        Assert.False(File.Exists(_vaultPath));
        return null;
    });

    [Fact]
    public Task ApplyPreferencesUsesExistingRecentVault() => Headless.RunAsync<object?>(async () =>
    {
        using (VaultService.CreateNew(_vaultPath, "master password"u8, FastOptions))
        {
        }

        var model = new UnlockViewModel();
        model.ApplyPreferences(new AppPreferences { LastVaultPath = _vaultPath });

        Assert.False(model.IsCreateMode);
        Assert.Equal(_vaultPath, model.VaultPath);
        return null;
    });

    [Fact]
    public Task ApplyPreferencesFallsBackWhenNoVaultExists() => Headless.RunAsync<object?>(async () =>
    {
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AegisVault",
            "vault.aegis");

        var model = new UnlockViewModel();
        model.ApplyPreferences(new AppPreferences { LastVaultPath = Path.Combine(_directory, "missing.aegis") });

        Assert.Equal(
            File.Exists(defaultPath) ? UnlockViewModel.OpenMode : UnlockViewModel.CreateMode,
            model.ModeIndex);
        return null;
    });

    [Fact]
    public Task CreateAppendsAegisExtension() => Headless.RunAsync<object?>(async () =>
    {
        var model = new UnlockViewModel
        {
            NewVaultPath = Path.Combine(_directory, "myvault"),
            ModeIndex = UnlockViewModel.CreateMode,
            MasterPassword = "master password",
            ConfirmPassword = "master password",
        };

        VaultService? created = null;
        model.VaultOpened += vault => created = vault;

        await model.CreateCommand.ExecuteAsync(null);

        Assert.Null(model.ErrorMessage);
        Assert.NotNull(created);
        Assert.EndsWith(".aegis", created!.VaultPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(_directory, "myvault.aegis")));
        created.Dispose();
        return null;
    });

    [Fact]
    public Task CreateRejectsExistingFile() => Headless.RunAsync<object?>(async () =>
    {
        var existing = Path.Combine(_directory, "existing.aegis");
        File.WriteAllText(existing, "not a vault");

        var model = new UnlockViewModel
        {
            NewVaultPath = existing,
            ModeIndex = UnlockViewModel.CreateMode,
            MasterPassword = "master password",
            ConfirmPassword = "master password",
        };

        await model.CreateCommand.ExecuteAsync(null);

        Assert.NotNull(model.ErrorMessage);
        Assert.Contains("已存在", model.ErrorMessage);
        return null;
    });
}
