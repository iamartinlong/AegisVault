using System.Text;
using AegisVault.App.Localization;
using AegisVault.App.Services;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AegisVault.App.ViewModels;

public partial class UnlockViewModel : ObservableObject
{
    public const int OpenMode = 0;
    public const int CreateMode = 1;

    public event Action<VaultService>? VaultOpened;

    public IKeyProtector? DeviceKeyProtector { get; set; }

    public bool CanRememberDevice => DeviceKeyProtector?.IsAvailable == true;

    public bool SecureInputAvailable { get; init; }

    /// <summary>0 = open an existing vault, 1 = create a new vault.</summary>
    [ObservableProperty]
    private int modeIndex;

    [ObservableProperty]
    private string vaultPath = GetDefaultVaultPath();

    [ObservableProperty]
    private string newVaultPath = GetDefaultVaultPath();

    [ObservableProperty]
    private string masterPassword = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private bool rememberDevice;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    private PasswordStrengthResult? _masterPasswordStrength;

    public bool IsCreateMode => ModeIndex == CreateMode;

    public string MasterPasswordStrengthSummary => _masterPasswordStrength is null
        ? string.Empty
        : StrengthFormatting.FormatSummary(
            _masterPasswordStrength.Score,
            _masterPasswordStrength.Label,
            _masterPasswordStrength.CrackTime);

    public double MasterPasswordStrengthPercent => (_masterPasswordStrength?.Score ?? 0) * 25;

    partial void OnMasterPasswordChanged(string value)
    {
        _masterPasswordStrength = string.IsNullOrEmpty(value)
            ? null
            : PasswordStrengthEstimator.Evaluate(value);

        OnPropertyChanged(nameof(MasterPasswordStrengthSummary));
        OnPropertyChanged(nameof(MasterPasswordStrengthPercent));
    }

    partial void OnModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCreateMode));
        ErrorMessage = null;
    }

    /// <summary>
    /// Applies persisted preferences: prefers the last vault (or the default
    /// path) when it exists, otherwise starts on the create tab.
    /// </summary>
    public void ApplyPreferences(AppPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var recent = preferences.LastVaultPath;
        if (!string.IsNullOrWhiteSpace(recent) && File.Exists(recent))
        {
            VaultPath = recent;
            ModeIndex = OpenMode;
            return;
        }

        var defaultPath = GetDefaultVaultPath();
        VaultPath = defaultPath;
        ModeIndex = File.Exists(defaultPath) ? OpenMode : CreateMode;
    }

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
            ErrorMessage = Loc.T("Unlock_ErrorPasswordRequired");
            return;
        }

        var path = VaultPath.Trim();
        if (!File.Exists(path))
        {
            ErrorMessage = Loc.T("Unlock_ErrorVaultNotFound");
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
                        ErrorMessage = Loc.T("Unlock_ErrorWrongPassword");
                        break;
                    case VaultUnlockStatus.UnsupportedVersion:
                        ErrorMessage = Loc.T("Unlock_ErrorUnsupportedVersion");
                        break;
                    default:
                        ErrorMessage = Loc.T("Unlock_ErrorCorrupted");
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
            ErrorMessage = Loc.T("Unlock_ErrorOpenFailed");
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
            ErrorMessage = Loc.T("Unlock_ErrorPasswordRequired");
            return;
        }

        if (!string.Equals(MasterPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = Loc.T("Unlock_ErrorConfirmMismatch");
            return;
        }

        if (MasterPassword.Length < 8)
        {
            ErrorMessage = Loc.T("Unlock_ErrorPasswordTooShort");
            return;
        }

        var path = NormalizeVaultPath(NewVaultPath);
        if (path.Length == 0)
        {
            ErrorMessage = Loc.T("Unlock_ErrorPathRequired");
            return;
        }

        if (File.Exists(path))
        {
            ErrorMessage = Loc.T("Unlock_ErrorFileExists");
            return;
        }

        NewVaultPath = path;

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
            ErrorMessage = Loc.T("Unlock_ErrorCreateFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal static string NormalizeVaultPath(string? raw)
    {
        var path = raw?.Trim() ?? string.Empty;
        if (path.Length == 0)
        {
            return string.Empty;
        }

        return path.EndsWith(".aegis", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ".aegis";
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
