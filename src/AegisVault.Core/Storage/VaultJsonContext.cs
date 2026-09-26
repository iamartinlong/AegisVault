using System.Text.Json.Serialization;
using AegisVault.Core.Models;

namespace AegisVault.Core.Storage;

/// <summary>
/// Source-generated JSON contexts (NativeAOT/trim safe).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(PasswordEntry))]
[JsonSerializable(typeof(List<PasswordEntry>))]
[JsonSerializable(typeof(KdfParameters))]
[JsonSerializable(typeof(UserConfig))]
[JsonSerializable(typeof(PasswordGeneratorOptions))]
[JsonSerializable(typeof(List<Category>))]
[JsonSerializable(typeof(CategoriesPayload))]
[JsonSerializable(typeof(ExportPayload))]
[JsonSerializable(typeof(EncryptedExport))]
internal sealed partial class VaultJsonContext : JsonSerializerContext;
