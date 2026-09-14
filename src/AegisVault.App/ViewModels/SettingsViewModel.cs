using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AegisVault.App.ViewModels;

public sealed record AutoLockOption(int Minutes, string Label);

public sealed record ClipboardOption(int Seconds, string Label);

public sealed record ThemeOption(string Value, string Label);

public partial class SettingsViewModel : ObservableObject
{
    private readonly VaultService _vault;
    private readonly SecureConfigService _config;
    private readonly IKeyProtector? _protector;
    private readonly Action<string>? _applyTheme;
    private readonly Action<bool>? _applyScreenGuard;
    private readonly Action<bool>? _applyFloatingBall;
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
        Action? imported = null)
    {
        _vault = vault;
        _config = config;
        _protector = protector;
        _applyTheme = applyTheme;
        _applyScreenGuard = applyScreenGuard;
        _applyFloatingBall = applyFloatingBall;
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

        DeviceKeySupported = protector?.IsAvailable == true;
        HasDeviceKey = DeviceKeySupported && vault.HasDeviceKey(protector!);

        ScreenGuardSupported = screenGuardSupported;
        DisableScreenCapture = user.DisableScreenCapture;
        ShowFloatingBall = showFloatingBall;
    }

    public IReadOnlyList<AutoLockOption> AutoLockOptions { get; } =
        [new(0, "关闭"), new(1, "1 分钟"), new(5, "5 分钟"), new(15, "15 分钟"), new(30, "30 分钟")];

    public IReadOnlyList<ClipboardOption> ClipboardOptions { get; } =
        [new(0, "不自动清除"), new(15, "15 秒"), new(30, "30 秒"), new(60, "1 分钟"), new(120, "2 分钟")];

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
        [new("system", "跟随系统"), new("light", "浅色"), new("dark", "深色")];

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

    [ObservableProperty]
    private bool generatorSymbols = true;

    [ObservableProperty]
    private bool generatorExcludeAmbiguous;

    [ObservableProperty]
    private ThemeOption? selectedTheme;

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
        StatusMessage = "设置已保存。";
    }

    [RelayCommand]
    private void ChangeMasterPassword()
    {
        StatusMessage = null;

        if (string.IsNullOrEmpty(NewMasterPassword))
        {
            StatusMessage = "请输入新主密码。";
            return;
        }

        if (NewMasterPassword.Length < 8)
        {
            StatusMessage = "主密码至少需要 8 个字符。";
            return;
        }

        if (!string.Equals(NewMasterPassword, ConfirmMasterPassword, StringComparison.Ordinal))
        {
            StatusMessage = "两次输入的密码不一致。";
            return;
        }

        var passwordBytes = Encoding.UTF8.GetBytes(NewMasterPassword);
        try
        {
            _vault.ChangeMasterPassword(passwordBytes);
            NewMasterPassword = string.Empty;
            ConfirmMasterPassword = string.Empty;
            HasDeviceKey = _protector is { IsAvailable: true } && _vault.HasDeviceKey(_protector);
            StatusMessage = "主密码已更改。设备密钥已重置，可重新“记住此设备”。";
        }
        catch (Exception)
        {
            StatusMessage = "更改主密码失败。";
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
            StatusMessage = "已记住此设备。";
        }
        catch (Exception)
        {
            StatusMessage = "无法记住此设备。";
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
        StatusMessage = "已移除此设备上的密钥。";
    }

    public void SaveBackupTo(string path)
    {
        try
        {
            _vault.SaveBackup(path);
            StatusMessage = "备份已保存。";
        }
        catch (Exception)
        {
            StatusMessage = "备份失败。";
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
            StatusMessage = $"导入完成：新增 {imported} 条，跳过 {skipped} 条。";
            _imported?.Invoke();
            return new ImportResult(imported, skipped);
        }
        catch (Exception)
        {
            StatusMessage = "导入失败：无法读取该 CSV 文件。";
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
