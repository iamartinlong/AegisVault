using AegisVault.App.ViewModels;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Xunit;

namespace AegisVault.App.Tests;

public sealed class UnlockViewModelDeviceTests : IDisposable
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

    public UnlockViewModelDeviceTests()
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
    public Task RememberDeviceStoresKeyOnCreate() => Headless.RunAsync<object?>(async () =>
    {
        var protector = new FakeKeyProtector();
        var model = new UnlockViewModel
        {
            NewVaultPath = _vaultPath,
            ModeIndex = UnlockViewModel.CreateMode,
            MasterPassword = "master password",
            ConfirmPassword = "master password",
            RememberDevice = true,
            DeviceKeyProtector = protector,
        };

        VaultService? created = null;
        model.VaultOpened += vault => created = vault;

        await model.CreateCommand.ExecuteAsync(null);

        Assert.NotNull(created);
        Assert.True(created!.HasDeviceKey(protector));
        created.Dispose();
        return null;
    });

    [Fact]
    public Task DeviceKeyAutoUnlockReturnsUnlockedVault() => Headless.RunAsync<object?>(async () =>
    {
        var protector = new FakeKeyProtector();

        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            vault.AddEntry(new PasswordEntry { Title = "GitHub" });
            vault.RememberDevice(protector);
        }

        var model = new UnlockViewModel
        {
            VaultPath = _vaultPath,
            DeviceKeyProtector = protector,
        };

        var raised = false;
        model.VaultOpened += _ => raised = true;

        var opened = await model.TryDeviceUnlockAsync();

        Assert.NotNull(opened);
        Assert.True(opened!.IsUnlocked);
        Assert.Single(opened.Entries);
        // The caller decides which window to show: the unlock window must not
        // be revealed through the VaultOpened event anymore.
        Assert.False(raised);
        opened.Dispose();
        return null;
    });

    [Fact]
    public Task DeviceKeyAutoUnlockReturnsNullWithoutKey() => Headless.RunAsync<object?>(async () =>
    {
        using (VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
        }

        var model = new UnlockViewModel
        {
            VaultPath = _vaultPath,
            DeviceKeyProtector = new FakeKeyProtector(),
        };

        var raised = false;
        model.VaultOpened += _ => raised = true;

        var opened = await model.TryDeviceUnlockAsync();

        Assert.Null(opened);
        Assert.False(raised);
        return null;
    });

    [Fact]
    public Task DeviceKeyAutoUnlockReturnsNullWhenVaultMissing() => Headless.RunAsync<object?>(async () =>
    {
        var model = new UnlockViewModel
        {
            VaultPath = _vaultPath,
            DeviceKeyProtector = new FakeKeyProtector(),
        };

        var opened = await model.TryDeviceUnlockAsync();

        Assert.Null(opened);
        return null;
    });

    [Fact]
    public Task DeviceKeyAutoUnlockReturnsNullWhenProtectorUnavailable() => Headless.RunAsync<object?>(async () =>
    {
        var protector = new FakeKeyProtector();

        using (var vault = VaultService.CreateNew(_vaultPath, Password, FastOptions))
        {
            vault.RememberDevice(protector);
        }

        protector.IsAvailable = false;
        var model = new UnlockViewModel
        {
            VaultPath = _vaultPath,
            DeviceKeyProtector = protector,
        };

        var opened = await model.TryDeviceUnlockAsync();

        Assert.Null(opened);
        return null;
    });
}
