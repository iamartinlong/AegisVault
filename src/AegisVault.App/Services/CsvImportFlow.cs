using AegisVault.App.Localization;
using AegisVault.Core.Services;

namespace AegisVault.App.Services;

/// <summary>
/// Shared CSV / Bitwarden import flow: parsing and vault writes run off the UI
/// thread, the completion message and the post-import refresh are marshalled
/// back by the caller's context. Used by the settings page and the empty-vault
/// hero shortcut.
/// </summary>
internal static class CsvImportFlow
{
    /// <summary>Import files above this size are refused (guards against runaway files).</summary>
    internal const long MaxImportBytes = 16L * 1024 * 1024;

    public static async Task<ImportResult> RunAsync(
        VaultService vault,
        string path,
        Action<string> setStatus,
        Action? afterImport = null)
    {
        ImportResult result;
        try
        {
            var length = new FileInfo(path).Length;
            if (length > MaxImportBytes)
            {
                setStatus(Loc.T("Settings_StatusImportTooLarge"));
                return new ImportResult(0, 0);
            }

            // Only the parsing (CPU + file IO) runs off-thread; the vault write
            // happens back on the caller's thread in one transaction, because
            // the vault owns a single non-thread-safe SQLite connection.
            var (entries, skipped) = await Task.Run(() => VaultCsvImporter.Parse(File.ReadAllText(path)));
            var added = vault.AddEntries(entries);
            result = new ImportResult(added.Count, skipped);
            setStatus(Loc.Format("Settings_StatusImportDone", result.Imported, result.Skipped));
        }
        catch (CsvImportLimitException)
        {
            setStatus(Loc.T("Settings_StatusImportTooLarge"));
            return new ImportResult(0, 0);
        }
        catch (Exception)
        {
            setStatus(Loc.T("Settings_StatusImportFailed"));
            return new ImportResult(0, 0);
        }

        try
        {
            afterImport?.Invoke();
        }
        catch (Exception)
        {
            // The entries are in the vault; a refresh failure must not be
            // reported as an import failure.
        }

        return result;
    }
}
