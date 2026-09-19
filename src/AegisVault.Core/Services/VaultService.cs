using System.Security.Cryptography;
using System.Text.Json;
using AegisVault.Core.Crypto;
using AegisVault.Core.Models;
using AegisVault.Core.Storage;
using Microsoft.Data.Sqlite;

namespace AegisVault.Core.Services;

public enum VaultUnlockStatus
{
    Success,
    WrongPassword,
    Corrupted,
    UnsupportedVersion,
}

/// <summary>
/// High-level vault API: create, unlock, lock, change master password and
/// entry CRUD. Key material (DEK) lives only in libsodium secure memory while
/// the vault is unlocked.
/// </summary>
public sealed class VaultService : IDisposable
{
    private const int DeviceKeyPayloadVersion = 1;
    private const int DeviceKeyPayloadSize = 1 + 32 + KeyEnvelope.DekSize;
    private const string CategoriesSettingKey = "categories";

    private static readonly byte[] CategoriesAssociatedData =
        System.Text.Encoding.UTF8.GetBytes("AegisVault|settings|categories|v2");

    /// <summary>v1 categories payload AAD (no colour field); read-only fallback.</summary>
    private static readonly byte[] CategoriesAssociatedDataV1 =
        System.Text.Encoding.UTF8.GetBytes("AegisVault|settings|categories|v1");

    private readonly VaultDatabase _database;
    private readonly List<PasswordEntry> _entries = [];
    private readonly List<Category> _categories = [];

    private VaultHeader _header;
    private SecureBuffer? _dek;
    private bool _disposed;

    private VaultService(VaultDatabase database, VaultHeader header)
    {
        _database = database;
        _header = header;
    }

    public bool IsUnlocked => _dek is not null;

    public string VaultPath => _database.Path;

    public IReadOnlyList<PasswordEntry> Entries => _entries;

    public IReadOnlyList<Category> Categories => _categories;

    /// <summary>Creates a new vault and returns it in the unlocked state.</summary>
    public static VaultService CreateNew(string path, ReadOnlySpan<byte> password, VaultOptions? options = null)
    {
        var database = VaultDatabase.OpenOrCreate(path);
        try
        {
            if (database.HasMeta())
            {
                throw new InvalidOperationException("A vault already exists at the specified path.");
            }

            var kdf = options?.Kdf ?? new KdfParameters();
            var derivation = KeyDerivationFactory.Create(kdf.Algorithm);
            var salt = RandomNumberGenerator.GetBytes(kdf.SaltSize);

            using var kek = derivation.DeriveKey(password, salt, kdf);

            var dekBytes = RandomNumberGenerator.GetBytes(KeyEnvelope.DekSize);
            try
            {
                var now = DateTimeOffset.UtcNow;
                var associatedData = VaultHeader.ComputeAssociatedData(VaultHeader.CurrentFormatVersion, kdf, salt);

                var header = new VaultHeader
                {
                    Kdf = kdf,
                    Salt = salt,
                    WrappedDek = KeyEnvelope.Wrap(kek.ReadOnlySpan, dekBytes, associatedData),
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                database.WriteMeta(header);

                var dek = SecureBuffer.From(dekBytes);
                dek.ProtectReadOnly();

                return new VaultService(database, header) { _dek = dek };
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dekBytes);
            }
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    /// <summary>Opens an existing vault file without unlocking it.</summary>
    public static VaultService Open(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException("The file is not an AegisVault vault (file not found).");
        }

        var database = VaultDatabase.OpenOrCreate(path);
        try
        {
            var header = database.ReadMeta()
                ?? throw new InvalidDataException("The file is not an AegisVault vault (metadata missing).");

            return new VaultService(database, header);
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    public VaultUnlockStatus Unlock(ReadOnlySpan<byte> password)
    {
        ThrowIfDisposed();

        if (_header.FormatVersion > VaultHeader.CurrentFormatVersion)
        {
            return VaultUnlockStatus.UnsupportedVersion;
        }

        IKeyDerivation derivation;
        try
        {
            derivation = KeyDerivationFactory.Create(_header.Kdf.Algorithm);
        }
        catch (NotSupportedException)
        {
            return VaultUnlockStatus.Corrupted;
        }

        SecureBuffer dek;
        try
        {
            using var kek = derivation.DeriveKey(password, _header.Salt, _header.Kdf);
            dek = KeyEnvelope.Unwrap(kek.ReadOnlySpan, _header.WrappedDek, _header.ComputeAssociatedData());
        }
        catch (CryptographicException)
        {
            return VaultUnlockStatus.WrongPassword;
        }

        return EstablishSession(dek);
    }

    /// <summary>Clears all decrypted state and zeroes the session key.</summary>
    public void Lock()
    {
        _entries.Clear();
        _categories.Clear();
        _dek?.Dispose();
        _dek = null;
    }

    /// <summary>Re-wraps the DEK under a new password (no entry re-encryption).</summary>
    public void ChangeMasterPassword(ReadOnlySpan<byte> newPassword, VaultOptions? options = null)
    {
        ThrowIfDisposed();
        EnsureUnlocked();

        var kdf = options?.Kdf ?? _header.Kdf;
        var derivation = KeyDerivationFactory.Create(kdf.Algorithm);
        var salt = RandomNumberGenerator.GetBytes(kdf.SaltSize);

        using var kek = derivation.DeriveKey(newPassword, salt, kdf);
        var associatedData = VaultHeader.ComputeAssociatedData(_header.FormatVersion, kdf, salt);

        var updated = new VaultHeader
        {
            FormatVersion = _header.FormatVersion,
            Kdf = kdf,
            Salt = salt,
            WrappedDek = KeyEnvelope.Wrap(kek.ReadOnlySpan, _dek!.ReadOnlySpan, associatedData),
            CreatedAt = _header.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _database.WriteMeta(updated);
        _header = updated;

        // Ciphertext of the wrapped DEK changed; remembered device keys no longer match.
        _database.DeleteAllDeviceKeys();
    }

    public PasswordEntry AddEntry(PasswordEntry entry)
    {
        ThrowIfDisposed();
        EnsureUnlocked();

        var now = DateTimeOffset.UtcNow;
        var item = entry with
        {
            Id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id,
            CreatedAt = entry.CreatedAt == default ? now : entry.CreatedAt,
            UpdatedAt = now,
        };

        Persist(item);
        _entries.Add(item);
        return item;
    }

    /// <summary>
    /// Adds many entries in a single transaction (bulk import). Runs on the
    /// calling thread so the vault's connection is never used concurrently;
    /// callers on a UI thread should parse off-thread and call this on the UI
    /// thread.
    /// </summary>
    public IReadOnlyList<PasswordEntry> AddEntries(IEnumerable<PasswordEntry> entries)
    {
        ThrowIfDisposed();
        EnsureUnlocked();
        ArgumentNullException.ThrowIfNull(entries);

        var now = DateTimeOffset.UtcNow;
        var items = new List<PasswordEntry>();
        foreach (var entry in entries)
        {
            items.Add(entry with
            {
                Id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id,
                CreatedAt = entry.CreatedAt == default ? now : entry.CreatedAt,
                UpdatedAt = now,
            });
        }

        if (items.Count == 0)
        {
            return items;
        }

        var records = new List<EntryRecord>(items.Count);
        foreach (var item in items)
        {
            var (nonce, ciphertext, tag) = EntryRepository.Encrypt(_dek!.ReadOnlySpan, item);
            records.Add(new EntryRecord(
                item.Id,
                EntryRepository.EntryFormatVersion,
                nonce,
                ciphertext,
                tag,
                item.UpdatedAt));
        }

        _database.WriteEntries(records);
        _entries.AddRange(items);
        return items;
    }

    public bool UpdateEntry(PasswordEntry entry)
    {
        ThrowIfDisposed();
        EnsureUnlocked();

        var index = _entries.FindIndex(existing => existing.Id == entry.Id);
        if (index < 0)
        {
            return false;
        }

        var item = entry with { UpdatedAt = DateTimeOffset.UtcNow };
        Persist(item);
        _entries[index] = item;
        return true;
    }

    public bool DeleteEntry(Guid id)
    {
        ThrowIfDisposed();
        EnsureUnlocked();

        var index = _entries.FindIndex(existing => existing.Id == id);
        if (index < 0)
        {
            return false;
        }

        _database.DeleteEntry(id);
        _entries.RemoveAt(index);
        return true;
    }

    /// <summary>Creates a category. Names are trimmed, non-empty and unique (case-insensitive).</summary>
    public Category AddCategory(string name, string? color = null)
    {
        ThrowIfDisposed();
        EnsureUnlocked();

        var normalized = NormalizeCategoryName(name);
        EnsureUniqueCategoryName(normalized, excludeId: null);

        var category = new Category { Name = normalized, Color = CategoryColors.Normalize(color) };
        _categories.Add(category);
        SortCategories();
        PersistCategories();
        return category;
    }

    public bool RenameCategory(Guid id, string name)
    {
        ThrowIfDisposed();
        EnsureUnlocked();

        var index = _categories.FindIndex(category => category.Id == id);
        if (index < 0)
        {
            return false;
        }

        var normalized = NormalizeCategoryName(name);
        EnsureUniqueCategoryName(normalized, excludeId: id);

        _categories[index] = _categories[index] with { Name = normalized };
        SortCategories();
        PersistCategories();
        return true;
    }

    /// <summary>Sets (or clears) the palette/hex colour of a category.</summary>
    public bool SetCategoryColor(Guid id, string? color)
    {
        ThrowIfDisposed();
        EnsureUnlocked();

        var index = _categories.FindIndex(category => category.Id == id);
        if (index < 0)
        {
            return false;
        }

        _categories[index] = _categories[index] with { Color = CategoryColors.Normalize(color) };
        PersistCategories();
        return true;
    }

    /// <summary>Deletes a category; its entries become uncategorized.</summary>
    public bool DeleteCategory(Guid id)
    {
        ThrowIfDisposed();
        EnsureUnlocked();

        var index = _categories.FindIndex(category => category.Id == id);
        if (index < 0)
        {
            return false;
        }

        _categories.RemoveAt(index);
        PersistCategories();

        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].CategoryId != id)
            {
                continue;
            }

            var item = _entries[i] with { CategoryId = null, UpdatedAt = DateTimeOffset.UtcNow };
            Persist(item);
            _entries[i] = item;
        }

        return true;
    }

    /// <summary>Encrypts a settings payload with the session key.</summary>
    public (byte[] Nonce, byte[] Ciphertext, byte[] Tag) EncryptSetting(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        ThrowIfDisposed();
        EnsureUnlocked();
        return AesGcmCipher.Encrypt(_dek!.ReadOnlySpan, plaintext, associatedData);
    }

    /// <summary>Decrypts a settings payload with the session key.</summary>
    public byte[] DecryptSetting(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> associatedData)
    {
        ThrowIfDisposed();
        EnsureUnlocked();
        return AesGcmCipher.Decrypt(_dek!.ReadOnlySpan, nonce, ciphertext, tag, associatedData);
    }

    public (byte[] Nonce, byte[] Ciphertext, byte[] Tag)? ReadSetting(string key)
    {
        ThrowIfDisposed();
        var record = _database.ReadSetting(key);
        return record is null ? null : (record.Nonce, record.Ciphertext, record.Tag);
    }

    public void WriteSetting(string key, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        ThrowIfDisposed();
        _database.WriteSetting(key, nonce, ciphertext, tag);
    }

    /// <summary>Writes a consistent copy of the encrypted database (defaults to <c>.bak</c>).</summary>
    public void SaveBackup(string? destinationPath = null)
    {
        ThrowIfDisposed();
        var destination = destinationPath ?? VaultPath + ".bak";
        _database.Checkpoint();
        File.Copy(VaultPath, destination, overwrite: true);
        VaultFilePermissions.Restrict(destination);
    }

    /// <summary>Whether a usable device key is stored for the given protector.</summary>
    public bool HasDeviceKey(IKeyProtector protector)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(protector);
        return protector.IsAvailable && _database.ReadDeviceKey(protector.Id) is not null;
    }

    /// <summary>
    /// Stores the session DEK protected by the OS key protector so the vault can
    /// be unlocked without the master password on this device.
    /// </summary>
    public void RememberDevice(IKeyProtector protector)
    {
        ThrowIfDisposed();
        EnsureUnlocked();
        ArgumentNullException.ThrowIfNull(protector);

        if (!protector.IsAvailable)
        {
            throw new InvalidOperationException("The key protector is not available on this machine.");
        }

        var payload = BuildDeviceKeyPayload();
        try
        {
            var blob = protector.Protect(payload);
            _database.UpsertDeviceKey(protector.Id, blob, DateTimeOffset.UtcNow);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public void ForgetDevice(IKeyProtector protector)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(protector);
        _database.DeleteDeviceKey(protector.Id);
    }

    /// <summary>
    /// Attempts to unlock using a previously remembered device key. Returns
    /// <c>false</c> when no key exists, it belongs to another vault state, or
    /// the entries cannot be decrypted.
    /// </summary>
    public bool TryUnlockWithDeviceKey(IKeyProtector protector)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(protector);

        if (!protector.IsAvailable || _header.FormatVersion > VaultHeader.CurrentFormatVersion)
        {
            return false;
        }

        var blob = _database.ReadDeviceKey(protector.Id);
        if (blob is null)
        {
            return false;
        }

        byte[] payload;
        try
        {
            payload = protector.Unprotect(blob);
        }
        catch (Exception exception) when (exception is CryptographicException or PlatformNotSupportedException)
        {
            return false;
        }

        try
        {
            if (payload.Length != DeviceKeyPayloadSize || payload[0] != DeviceKeyPayloadVersion)
            {
                return false;
            }

            var expected = SHA256.HashData(_header.WrappedDek);
            if (!CryptographicOperations.FixedTimeEquals(payload.AsSpan(1, 32), expected))
            {
                return false;
            }

            var dek = SecureBuffer.From(payload.AsSpan(33, KeyEnvelope.DekSize));
            return EstablishSession(dek) == VaultUnlockStatus.Success;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Lock();
        _database.Dispose();
    }

    private List<PasswordEntry> LoadEntries(SecureBuffer dek, List<PasswordEntry> outdated)
    {
        var loaded = new List<PasswordEntry>();
        foreach (var record in _database.ReadEntries())
        {
            if (record.Version > EntryRepository.EntryFormatVersion)
            {
                throw new UnsupportedVaultVersionException(
                    $"Entry format version {record.Version} is newer than this application supports.");
            }

            var entry = EntryRepository.Decrypt(
                dek.ReadOnlySpan,
                record.Id,
                record.Version,
                record.Nonce,
                record.Ciphertext,
                record.Tag);

            loaded.Add(entry);

            if (record.Version < EntryRepository.EntryFormatVersion)
            {
                outdated.Add(entry);
            }
        }

        return loaded;
    }

    /// <summary>
    /// Re-encrypts entries whose payload was written by an older format so the
    /// upgrade is persisted (one-time, best effort: a failed rewrite leaves the
    /// in-memory upgrade intact and is retried on the next unlock).
    /// </summary>
    private void PersistOutdatedEntries(List<PasswordEntry> outdated, SecureBuffer dek)
    {
        if (outdated.Count == 0)
        {
            return;
        }

        try
        {
            var records = new List<EntryRecord>(outdated.Count);
            foreach (var entry in outdated)
            {
                var (nonce, ciphertext, tag) = EntryRepository.Encrypt(dek.ReadOnlySpan, entry);
                records.Add(new EntryRecord(
                    entry.Id,
                    EntryRepository.EntryFormatVersion,
                    nonce,
                    ciphertext,
                    tag,
                    entry.UpdatedAt));
            }

            _database.WriteEntries(records);
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            // The vault stays usable; the payloads are upgraded in memory and
            // will be written back on the next unlock or entry save.
        }
    }

    private void Persist(PasswordEntry entry)
    {
        var (nonce, ciphertext, tag) = EntryRepository.Encrypt(_dek!.ReadOnlySpan, entry);
        _database.UpsertEntry(entry.Id, EntryRepository.EntryFormatVersion, nonce, ciphertext, tag, entry.UpdatedAt);
    }

    private void PersistCategories()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(_categories, VaultJsonContext.Default.ListCategory);
        try
        {
            var (nonce, ciphertext, tag) = EncryptSetting(json, CategoriesAssociatedData);
            WriteSetting(CategoriesSettingKey, nonce, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    /// <summary>
    /// Reads the encrypted categories blob. Tries the current AAD first and falls
    /// back to the v1 AAD, reporting whether the payload has to be written back
    /// in the current format.
    /// </summary>
    private (List<Category> Categories, bool Upgraded) LoadCategories()
    {
        var stored = ReadSetting(CategoriesSettingKey);
        if (stored is null)
        {
            return ([], false);
        }

        var upgraded = false;
        byte[] json;
        try
        {
            json = DecryptSetting(
                stored.Value.Nonce,
                stored.Value.Ciphertext,
                stored.Value.Tag,
                CategoriesAssociatedData);
        }
        catch (CryptographicException)
        {
            try
            {
                json = DecryptSetting(
                    stored.Value.Nonce,
                    stored.Value.Ciphertext,
                    stored.Value.Tag,
                    CategoriesAssociatedDataV1);
                upgraded = true;
            }
            catch (CryptographicException)
            {
                // A corrupt categories blob must never break unlocking; fall back to none.
                return ([], false);
            }
        }

        try
        {
            var categories = JsonSerializer.Deserialize(json, VaultJsonContext.Default.ListCategory) ?? [];
            return (categories.Where(category => category is not null)
                .Select(ModelMigrations.Normalize)
                .ToList(), upgraded);
        }
        catch (JsonException)
        {
            return ([], false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    private void SortCategories()
        => _categories.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeCategoryName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Category name must not be empty.", nameof(name));
        }

        return trimmed;
    }

    private void EnsureUniqueCategoryName(string name, Guid? excludeId)
    {
        if (_categories.Any(category =>
                category.Id != excludeId &&
                string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"A category named '{name}' already exists.", nameof(name));
        }
    }

    private void EnsureUnlocked()
    {
        if (!IsUnlocked)
        {
            throw new InvalidOperationException("The vault is locked.");
        }
    }

    private byte[] BuildDeviceKeyPayload()
    {
        var payload = new byte[DeviceKeyPayloadSize];
        payload[0] = DeviceKeyPayloadVersion;
        SHA256.HashData(_header.WrappedDek).CopyTo(payload.AsSpan(1, 32));
        _dek!.ReadOnlySpan.CopyTo(payload.AsSpan(33));
        return payload;
    }

    private VaultUnlockStatus EstablishSession(SecureBuffer dek)
    {
        List<PasswordEntry> loaded;
        List<PasswordEntry> outdated = [];
        try
        {
            loaded = LoadEntries(dek, outdated);
        }
        catch (UnsupportedVaultVersionException)
        {
            // A newer payload version must be reported as such, not as damage.
            dek.Dispose();
            return VaultUnlockStatus.UnsupportedVersion;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or JsonException or InvalidDataException)
        {
            dek.Dispose();
            return VaultUnlockStatus.Corrupted;
        }

        dek.ProtectReadOnly();

        _entries.Clear();
        _entries.AddRange(loaded);

        _dek?.Dispose();
        _dek = dek;

        // Persist any payload upgrades now that the session key is in place.
        PersistOutdatedEntries(outdated, dek);

        // Categories are decrypted with the session key, so load them only after
        // the DEK is in place.
        _categories.Clear();
        var (categories, upgraded) = LoadCategories();
        _categories.AddRange(categories);

        if (upgraded)
        {
            // One-shot best-effort rewrite at the current AAD version; a failure
            // simply retries on the next unlock.
            try
            {
                PersistCategories();
            }
            catch (Exception exception) when (exception is SqliteException or IOException)
            {
            }
        }

        return VaultUnlockStatus.Success;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
