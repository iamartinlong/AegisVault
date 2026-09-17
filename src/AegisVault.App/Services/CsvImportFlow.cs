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
    public static async Task<ImportResult> RunAsync(
        VaultService vault,
        string path,
        Action<string> setStatus,
        Action? afterImport = null)
    {
        try
        {
            var (imported, skipped) = await Task.Run(() => VaultCsvImporter.Import(vault, File.ReadAllText(path)));
            setStatus(Loc.Format("Settings_StatusImportDone", imported, skipped));
            afterImport?.Invoke();
            return new ImportResult(imported, skipped);
        }
        catch (Exception)
        {
            setStatus(Loc.T("Settings_StatusImportFailed"));
            return new ImportResult(0, 0);
        }
    }
}
