using System.Text.Json.Serialization;

namespace TheHaunt.Core;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GameData))]
public sealed partial class SaveJsonContext : JsonSerializerContext { }
