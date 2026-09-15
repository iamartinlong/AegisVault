using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AegisVault.App.Localization;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AegisVault.App.ViewModels;

public sealed record AutoLockOption(int Minutes, string Label);

public sealed record ClipboardOption(int Seconds, string Label);

public sealed record ThemeOption(string Value, string Label);

public sealed record LanguageOption(string Value, string Label);

public partial class SettingsViewModel : ObservableObject
{
    private readonly VaultService _vault;
    private readonly SecureConfigService _config;
    private readonly IKeyProtector? _protector;
    private readonly Action<string>? _applyTheme;
    private readonly Action<bool>? _applyScreenGuard;
    private readonly Action<bool>? _applyFloatingBall;
    private readonly Action<string>? _applyLanguage;
    private readonly Action? _imported;

    public SettingsViewModel(
        VaultService vault,
        SecureConfigService config,
        IKeyProtector? protector,
        Action<string>? applyTheme,
        string currentTheme,
        bool screenGuardSupported = false,
        Action<bool>? applyScreenGuard = null,
        bool showFloatingBall = false,
        Action<bool>? applyFloatingBall = null,
        Action? imported = null,
        string currentLanguage = Loc.System,
        Action<string>? applyLanguage = null)
    {
        _vault = vault;
        _config = config;
        _protector = protector;
        _applyTheme = applyTheme;
        _applyScreenGuard = applyScreenGuard;
        _applyFloatingBall = applyFloatingBall;
        _applyLanguage = applyLanguage;
        _imported = imported;

        var user = config.Current;
        SelectedAutoLock = AutoLockOptions.FirstOrDefault(option => option.Minutes == user.AutoLockMinutes) ?? AutoLockOptions[0];
        LockOnMinimize = user.LockOnMinimize;
        LockOnScreenLock = user.LockOnScreenLock;
        LockOnSuspend = user.LockOnSuspend;
        SelectedClipboard = ClipboardOptions.FirstOrDefault(option => option.Seconds == user.ClipboardClearSeconds) ?? ClipboardOptions[0];
        GeneratorLength = user.Generator.Length;
        GeneratorSymbols = user.Generator.IncludeSymbols;
        GeneratorExcludeAmbiguous = user.Generator.ExcludeAmbiguous;
        SelectedTheme = ThemeOptions.FirstOrDefault(option => option.Value == currentTheme) ?? ThemeOptions[0];
        SelectedLanguage = LanguageOptions.FirstOrDefault(option => option.Value == currentLanguage) ?? LanguageOptions[0];

        DeviceKeySupported = protector?.IsAvailable == true;
        HasDeviceKey = DeviceKeySupported && vault.HasDeviceKey(protector!);

        ScreenGuardSupported = screenGuardSupported;
        DisableScreenCapture = user.DisableScreenCapture;
        ShowFloatingBall = showFloatingBall;
    }

    public IReadOnlyList<AutoLockOption> AutoLockOptions { get; } =
        [new(0, Loc.T("Settings_Off")),
         new(1, Loc.Format("Settings_Minutes", 1)),
         new(5, Loc.Format("Settings_Minutes", 5)),
         new(15, Loc.Format("Settings_Minutes", 15)),
         new(30, Loc.Format("Settings_Minutes", 30))];

    public IReadOnlyList<ClipboardOption> ClipboardOptions { get; } =
        [new(0, Loc.T("Settings_ClipboardOff")),
         new(15, Loc.Format("Settings_Seconds", 15)),
         new(30, Loc.Format("Settings_Seconds", 30)),
         new(60, Loc.Format("Settings_Minutes", 1)),
         new(120, Loc.Format("Settings_Minutes", 2))];

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
        [new("system", Loc.T("Settings_ThemeSystem")),
         new("light", Loc.T("Settings_ThemeLight")),
         new("dark", Loc.T("Settings_ThemeDark"))];

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
        [new(Loc.System, Loc.T("Settings_LanguageSystem")),
         new(Loc.Chinese, Loc.T("Settings_LanguageZh")),
         new(Loc.English, Loc.T("Settings_LanguageEn"))];

    [ObservableProperty]
    private AutoLockOption? selectedAutoLock;

    [ObservableProperty]
    private bool lockOnMinimize;

    [ObservableProperty]
    private bool lockOnScreenLock;

    [ObservableProperty]
    private bool lockOnSuspend;

    [ObservableProperty]
    private ClipboardOption? selectedClipboard;

    [ObservableProperty]
    private double generatorLength = 20;

    public string GeneratorLengthText => Loc.Format("Settings_GeneratorLength", GeneratorLength);

    partial void OnGeneratorLengthChanged(double value) => OnPropertyChanged(nameof(GeneratorLengthText));

    [ObservableProperty]
    private bool generatorSymbols = true;

    [ObservableProperty]
    private bool generatorExcludeAmbiguous;

    [ObservableProperty]
    private ThemeOption? selectedTheme;

    [ObservableProperty]
    private LanguageOption? selectedLanguage;

    [ObservableProperty]
    private bool screenGuardSupported;

    [ObservableProperty]
    private bool disableScreenCapture;

    [ObservableProperty]
    private bool showFloatingBall;

    [ObservableProperty]
    private bool deviceKeySupported;

    [ObservableProperty]
    private bool hasDeviceKey;

    [ObservableProperty]
    private string newMasterPassword = string.Empty;

    [ObservableProperty]
    private string confirmMasterPassword = string.Empty;

    [ObservableProperty]
    private string? statusMessage;

    public string VersionText =>
        $"AegisVault {typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0"}";

    [RelayCommand]
    private void Save()
    {
        var current = _config.Current;
        var updated = current with
        {
            AutoLockMinutes = SelectedAutoLock?.Minutes ?? current.AutoLockMinutes,
            LockOnMinimize = LockOnMinimize,
            LockOnScreenLock = LockOnScreenLock,
            LockOnSuspend = LockOnSuspend,
            ClipboardClearSeconds = SelectedClipboard?.Seconds ?? current.ClipboardClearSeconds,
            DisableScreenCapture = DisableScreenCapture,
            Generator = current.Generator with
            {
                Length = (int)Math.Round(GeneratorLength),
                IncludeSymbols = GeneratorSymbols,
                ExcludeAmbiguous = GeneratorExcludeAmbiguous,
            },
        };

        _config.Save(updated);
        _applyTheme?.Invoke(SelectedTheme?.Value ?? "system");
        _applyScreenGuard?.Invoke(!DisableScreenCapture);
        _applyFloatingBall?.Invoke(ShowFloatingBall);
        _applyLanguage?.Invoke(SelectedLanguage?.Value ?? Loc.System);
        StatusMessage = Loc.T("Settings_StatusSaved");
    }

    [RelayCommand]
    private void ChangeMasterPassword()
    {
        StatusMessage = null;

        if (string.IsNullOrEmpty(NewMasterPassword))
        {
            StatusMessage = Loc.T("Settings_StatusPasswordRequired");
            return;
        }

        if (NewMasterPassword.Length < 8)
        {
            StatusMessage = Loc.T("Settings_StatusPasswordTooShort");
            return;
        }

        if (!string.Equals(NewMasterPassword, ConfirmMasterPassword, StringComparison.Ordinal))
        {
            StatusMessage = Loc.T("Settings_StatusPasswordMismatch");
            return;
        }

        var passwordBytes = Encoding.UTF8.GetBytes(NewMasterPassword);
        try
        {
            _vault.ChangeMasterPassword(passwordBytes);
            NewMasterPassword = string.Empty;
            ConfirmMasterPassword = string.Empty;
            HasDeviceKey = _protector is { IsAvailable: true } && _vault.HasDeviceKey(_protector);
            StatusMessage = Loc.T("Settings_StatusPasswordChanged");
        }
        catch (Exception)
        {
            StatusMessage = Loc.T("Settings_StatusPasswordChangeFailed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    [RelayCommand]
    private void RememberDevice()
    {
        if (_protector is not { IsAvailable: true } protector)
        {
            return;
        }

        try
        {
            _vault.RememberDevice(protector);
            HasDeviceKey = true;
            StatusMessage = Loc.T("Settings_StatusDeviceRemembered");
        }
        catch (Exception)
        {
            StatusMessage = Loc.T("Settings_StatusDeviceRememberFailed");
        }
    }

    [RelayCommand]
    private void ForgetDevice()
    {
        if (_protector is not { } protector)
        {
            return;
        }

        _vault.ForgetDevice(protector);
        HasDeviceKey = false;
        StatusMessage = Loc.T("Settings_StatusDeviceForgotten");
    }

    public void SaveBackupTo(string path)
    {
        try
        {
            _vault.SaveBackup(path);
            StatusMessage = Loc.T("Settings_StatusBackupSaved");
        }
        catch (Exception)
        {
            StatusMessage = Loc.T("Settings_StatusBackupFailed");
        }
    }

    /// <summary>Imports entries from a CSV file (Bitwarden or generic layout).</summary>
    public async Task<ImportResult> ImportCsvFromAsync(string path)
    {
        try
        {
            // Parsing + vault writes run off the UI thread; the completion
            // message and reload marshalled back by the callers.
            var (imported, skipped) = await Task.Run(() => VaultCsvImporter.Import(_vault, File.ReadAllText(path)));
            StatusMessage = Loc.Format("Settings_StatusImportDone", imported, skipped);
            _imported?.Invoke();
            return new ImportResult(imported, skipped);
        }
        catch (Exception)
        {
            StatusMessage = Loc.T("Settings_StatusImportFailed");
            return new ImportResult(0, 0);
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            var vaultPath = _vault.VaultPath;
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{vaultPath}\"") { UseShellExecute = true });
            }
            else
            {
                var directory = Path.GetDirectoryName(vaultPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
                }
            }
        }
        catch (Exception)
        {
        }
    }
}
