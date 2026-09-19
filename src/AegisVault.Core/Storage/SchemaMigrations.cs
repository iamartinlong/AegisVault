using Microsoft.Data.Sqlite;

namespace AegisVault.Core.Storage;

/// <summary>Versioned schema creation and upgrades.</summary>
internal static class SchemaMigrations
{
    public const int CurrentSchemaVersion = 2;

    public static void Apply(SqliteConnection connection)
    {
        var version = ReadUserVersion(connection);

        if (version == CurrentSchemaVersion)
        {
            return;
        }

        if (version > CurrentSchemaVersion)
        {
            throw new AegisVault.Core.Services.UnsupportedVaultVersionException(
                $"Vault schema version {version} is newer than this application supports ({CurrentSchemaVersion}).");
        }

        using var transaction = connection.BeginTransaction();

        if (version < 1)
        {
            Execute(connection, transaction, """
                CREATE TABLE vault_meta (
                  id             INTEGER PRIMARY KEY CHECK (id = 1),
                  format_version INTEGER NOT NULL,
                  kdf_algorithm  TEXT    NOT NULL,
                  kdf_salt       BLOB    NOT NULL,
                  kdf_params     TEXT    NOT NULL,
                  wrapped_dek    BLOB    NOT NULL,
                  created_at     TEXT    NOT NULL,
                  updated_at     TEXT    NOT NULL
                );

                CREATE TABLE entries (
                  id         TEXT PRIMARY KEY,
                  nonce      BLOB    NOT NULL,
                  ciphertext BLOB    NOT NULL,
                  tag        BLOB    NOT NULL,
                  version    INTEGER NOT NULL,
                  updated_at TEXT    NOT NULL
                );

                CREATE TABLE settings (
                  key        TEXT PRIMARY KEY,
                  nonce      BLOB    NOT NULL,
                  ciphertext BLOB    NOT NULL,
                  tag        BLOB    NOT NULL
                );
                """);
        }

        if (version < 2)
        {
            Execute(connection, transaction, """
                CREATE TABLE device_keys (
                  protector  TEXT PRIMARY KEY,
                  blob       BLOB NOT NULL,
                  created_at TEXT NOT NULL
                );
                """);
        }

        Execute(connection, transaction, $"PRAGMA user_version = {CurrentSchemaVersion};");
        transaction.Commit();
    }

    private static int ReadUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
