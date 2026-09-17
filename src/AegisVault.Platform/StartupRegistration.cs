using Microsoft.Win32;

namespace AegisVault.Platform;

/// <summary>Registers the application to start with the operating system.</summary>
public interface IStartupRegistration
{
    /// <summary>Whether the platform supports auto-start registration.</summary>
    bool IsSupported { get; }

    /// <summary>Whether an entry for this executable currently exists (OS is the source of truth).</summary>
    bool IsEnabled(string executablePath);

    /// <summary>Creates or removes the entry; false when the OS refused (permissions).</summary>
    bool TrySetEnabled(bool enabled, string executablePath, string? arguments = null);
}

/// <summary>
/// Windows auto-start through <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// The current user's hive needs no elevation; a locked-down machine can still
/// refuse the write, which the caller surfaces to the user.
/// </summary>
public sealed class WindowsStartupRegistration : IStartupRegistration
{
    public const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "AegisVault";

    private readonly string _keyPath;

    /// <param name="keyPath">
    /// Registry sub-key (HKCU). Tests point this at a disposable key; the app
    /// uses <see cref="DefaultKeyPath"/>.
    /// </param>
    public WindowsStartupRegistration(string? keyPath = null)
        => _keyPath = keyPath ?? DefaultKeyPath;

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsEnabled(string executablePath)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false);
            var command = key?.GetValue(ValueName) as string;
            return command is not null &&
                   command.Contains(executablePath.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public bool TrySetEnabled(bool enabled, string executablePath, string? arguments = null)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            if (!enabled)
            {
                using var existing = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
                existing?.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            using var key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
            if (key is null)
            {
                return false;
            }

            var command = string.IsNullOrWhiteSpace(arguments)
                ? $"\"{executablePath.Trim()}\""
                : $"\"{executablePath.Trim()}\" {arguments.Trim()}";
            key.SetValue(ValueName, command, RegistryValueKind.String);
            return true;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
