using System.Text.Json.Nodes;

namespace TheHaunt.Core;

/// <summary>
/// v7 -> v8 (the farm's map id rename): <c>"test_farm"</c> becomes <c>"farm"</c>.
/// A v7 save holds a map id in exactly three places, each rewritten here:
/// <c>Player.MapId</c>, the keys of <c>Maps</c> (the farm's tiles and objects move
/// to the <c>"farm"</c> key only if that key is absent — both present touches
/// neither), and <c>Scooter.MapId</c>. No other save field holds a map id
/// (story flags, storage ids, garage jobs, tile and object records carry none).
/// A missing or malformed part is skipped; load repair owns degenerate saves.
///
/// FROZEN — the launch-era shape this migrates between never changes again.
/// </summary>
public sealed class MigrationV7ToV8 : ISaveMigration
{
    public int FromVersion => 7;

    public void Apply(JsonNode root)
    {
        if (root["Player"] is JsonObject player)
        {
            RenameMapId(player);
        }
        if (root["Maps"] is JsonObject maps
            && maps.ContainsKey("test_farm")
            && !maps.ContainsKey("farm"))
        {
            JsonNode? farm = maps["test_farm"];
            maps.Remove("test_farm");
            maps["farm"] = farm;
        }
        if (root["Scooter"] is JsonObject scooter)
        {
            RenameMapId(scooter);
        }
    }

    private static void RenameMapId(JsonObject owner)
    {
        if (owner["MapId"] is JsonValue id
            && id.TryGetValue(out string? mapId)
            && mapId == "test_farm")
        {
            owner["MapId"] = "farm";
        }
    }
}
