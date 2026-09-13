using System.Text;
using AegisVault.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AegisVault.App.ViewModels;

public partial class UnlockViewModel : ObservableObject
{
    public event Action<VaultService>? VaultOpened;

    public IKeyProtector? DeviceKeyProtector { get; set; }

    public bool CanRememberDevice => DeviceKeyProtector?.IsAvailable == true;

    public bool SecureInputAvailable { get; init; }

    [ObservableProperty]
    private string vaultPath = GetDefaultVaultPath();

    [ObservableProperty]
    private string masterPassword = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private bool createNew;

    [ObservableProperty]
    private bool rememberDevice;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    /// <summary>
    /// Attempts a password-less unlock using the remembered device key.
    /// Raises <see cref="VaultOpened"/> on success; otherwise does nothing.
    /// </summary>
    public async Task TryDeviceUnlockAsync()
    {
        if (DeviceKeyProtector is not { IsAvailable: true } protector)
        {
            return;
        }

        var path = VaultPath.Trim();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        VaultService? vault = null;
        var unlocked = false;

        await Task.Run(() =>
        {
            try
            {
                vault = VaultService.Open(path);
                unlocked = vault.TryUnlockWithDeviceKey(protector);
                if (!unlocked)
                {
                    vault.Dispose();
                    vault = null;
                }
            }
            catch (Exception)
            {
                vault?.Dispose();
                vault = null;
                unlocked = false;
            }
        });

        if (unlocked)
        {
            VaultOpened?.Invoke(vault!);
        }
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        if (string.IsNullOrEmpty(MasterPassword))
        {
            ErrorMessage = "请输入主密码。";
            return;
        }

        var path = VaultPath.Trim();
        if (!File.Exists(path))
        {
            ErrorMessage = "找不到密码库文件，请检查路径。";
            return;
        }

        IsBusy = true;
        try
        {
            var passwordBytes = Encoding.UTF8.GetBytes(MasterPassword);
            try
            {
                VaultService? vault = null;
                var status = VaultUnlockStatus.Corrupted;

                await Task.Run(() =>
                {
                    vault = VaultService.Open(path);
                    status = vault.Unlock(passwordBytes);
                    if (status != VaultUnlockStatus.Success)
                    {
                        vault.Dispose();
                        vault = null;
                    }
                });

                switch (status)
                {
                    case VaultUnlockStatus.Success:
                        MasterPassword = string.Empty;
                        RememberDeviceIfRequested(vault!);
                        VaultOpened?.Invoke(vault!);
                        break;
                    case VaultUnlockStatus.WrongPassword:
                        ErrorMessage = "主密码错误。";
                        break;
                    case VaultUnlockStatus.UnsupportedVersion:
                        ErrorMessage = "该密码库由更高版本的 AegisVault 创建，请升级应用。";
                        break;
                    default:
                        ErrorMessage = "密码库文件已损坏或无法识别。";
                        break;
                }
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }
        catch (Exception)
        {
            ErrorMessage = "无法打开密码库。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        if (string.IsNullOrEmpty(MasterPassword))
        {
            ErrorMessage = "请输入主密码。";
            return;
        }

        if (!string.Equals(MasterPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "两次输入的密码不一致。";
            return;
        }

        if (MasterPassword.Length < 8)
        {
            ErrorMessage = "主密码至少需要 8 个字符。";
            return;
        }

        var path = VaultPath.Trim();
        if (File.Exists(path))
        {
            ErrorMessage = "该路径已存在文件，请更换路径或直接解锁。";
            return;
        }

        IsBusy = true;
        try
        {
            var passwordBytes = Encoding.UTF8.GetBytes(MasterPassword);
            try
            {
                VaultService? vault = null;
                await Task.Run(() => vault = VaultService.CreateNew(path, passwordBytes));

                MasterPassword = string.Empty;
                ConfirmPassword = string.Empty;
                RememberDeviceIfRequested(vault!);
                VaultOpened?.Invoke(vault!);
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }
        catch (Exception)
        {
            ErrorMessage = "无法创建密码库。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RememberDeviceIfRequested(VaultService vault)
    {
        if (!RememberDevice || DeviceKeyProtector is not { IsAvailable: true } protector)
        {
            return;
        }

        try
        {
            vault.RememberDevice(protector);
        }
        catch (Exception)
        {
        }
    }

    private static string GetDefaultVaultPath()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "AegisVault", "vault.aegis");
    }
}
