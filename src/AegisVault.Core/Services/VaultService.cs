using System.Security.Cryptography;
using System.Text.Json;
using AegisVault.Core.Crypto;
using AegisVault.Core.Models;
using AegisVault.Core.Storage;

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
    private readonly VaultDatabase _database;
    private readonly List<PasswordEntry> _entries = [];

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

        List<PasswordEntry> loaded;
        try
        {
            loaded = LoadEntries(dek);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or InvalidDataException)
        {
            dek.Dispose();
            return VaultUnlockStatus.Corrupted;
        }

        dek.ProtectReadOnly();

        _entries.Clear();
        _entries.AddRange(loaded);

        _dek?.Dispose();
        _dek = dek;
        return VaultUnlockStatus.Success;
    }

    /// <summary>Clears all decrypted state and zeroes the session key.</summary>
    public void Lock()
    {
        _entries.Clear();
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

    private List<PasswordEntry> LoadEntries(SecureBuffer dek)
    {
        var loaded = new List<PasswordEntry>();
        foreach (var record in _database.ReadEntries())
        {
            if (record.Version > EntryRepository.EntryFormatVersion)
            {
                throw new InvalidDataException(
                    $"Entry format version {record.Version} is newer than this application supports.");
            }

            loaded.Add(EntryRepository.Decrypt(
                dek.ReadOnlySpan,
                record.Id,
                record.Version,
                record.Nonce,
                record.Ciphertext,
                record.Tag));
        }

        return loaded;
    }

    private void Persist(PasswordEntry entry)
    {
        var (nonce, ciphertext, tag) = EntryRepository.Encrypt(_dek!.ReadOnlySpan, entry);
        _database.UpsertEntry(entry.Id, EntryRepository.EntryFormatVersion, nonce, ciphertext, tag, entry.UpdatedAt);
    }

    private void EnsureUnlocked()
    {
        if (!IsUnlocked)
        {
            throw new InvalidOperationException("The vault is locked.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
