using System.Text.Json.Nodes;

namespace TheHaunt.Core;

// Frozen v2 → v3 upgrade: adds the empty StoryFlags map, only if absent.
// Deliberately does NOT call live code — migrations are frozen history. Recorded
// consequence: migrated v1/v2 saves get empty flags and replay the intro with the
// road re-blocked, even with planted crops. Accepted for dev-only saves [KEVIN].
public sealed class MigrationV2ToV3 : ISaveMigration
{
    public int FromVersion => 2;

    public void Apply(JsonNode root)
    {
        if (root["StoryFlags"] is null)
        {
            root["StoryFlags"] = new JsonObject();
        }
    }
}
