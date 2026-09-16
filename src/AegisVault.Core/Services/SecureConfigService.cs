using System.Text;
using System.Text.Json;
using AegisVault.Core.Models;
using AegisVault.Core.Storage;

namespace AegisVault.Core.Services;

/// <summary>
/// Lazily initialized, encrypted user configuration.
/// Nothing is decrypted until <see cref="Current"/> is first accessed,
/// satisfying the "no early decryption at startup" requirement.
/// </summary>
public sealed class SecureConfigService
{
    private const string SettingKey = "user-config";

    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("AegisVault|settings|user-config|v1");

    private readonly VaultService _vault;
    private readonly Lock _gate = new();

    private UserConfig? _config;

    public SecureConfigService(VaultService vault)
    {
        ArgumentNullException.ThrowIfNull(vault);
        _vault = vault;
    }

    /// <summary>Loads (once) and returns the configuration.</summary>
    public UserConfig Current
    {
        get
        {
            lock (_gate)
            {
                return _config ??= Load();
            }
        }
    }

    public void Save(UserConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_gate)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(config, VaultJsonContext.Default.UserConfig);
            try
            {
                var (nonce, ciphertext, tag) = _vault.EncryptSetting(json, AssociatedData);
                _vault.WriteSetting(SettingKey, nonce, ciphertext, tag);
                _config = config;
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(json);
            }
        }
    }

    private UserConfig Load()
    {
        var stored = _vault.ReadSetting(SettingKey);
        if (stored is null)
        {
            return new UserConfig();
        }

        byte[] json;
        try
        {
            json = _vault.DecryptSetting(
                stored.Value.Nonce,
                stored.Value.Ciphertext,
                stored.Value.Tag,
                AssociatedData);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // A corrupt settings blob must never break unlocking; fall back to defaults.
            return new UserConfig();
        }

        try
        {
            var config = JsonSerializer.Deserialize(json, VaultJsonContext.Default.UserConfig);
            return config is null ? new UserConfig() : ModelMigrations.Normalize(config);
        }
        catch (JsonException)
        {
            // A corrupt settings blob must never break unlocking; fall back to defaults.
            return new UserConfig();
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(json);
        }
    }
}
