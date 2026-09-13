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
            VaultPath = _vaultPath,
            MasterPassword = "master password",
            ConfirmPassword = "master password",
            CreateNew = true,
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

        Assert.Equal("找不到密码库文件，请检查路径。", model.ErrorMessage);
        return null;
    });

    [Fact]
    public Task MismatchedConfirmationShowsError() => Headless.RunAsync<object?>(async () =>
    {
        var model = new UnlockViewModel
        {
            VaultPath = _vaultPath,
            MasterPassword = "master password",
            ConfirmPassword = "different",
            CreateNew = true,
        };

        await model.CreateCommand.ExecuteAsync(null);

        Assert.Equal("两次输入的密码不一致。", model.ErrorMessage);
        Assert.False(File.Exists(_vaultPath));
        return null;
    });
}
