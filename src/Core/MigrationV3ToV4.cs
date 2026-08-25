using System.Text.Json.Nodes;

namespace TheHaunt.Core;

// Frozen v3 → v4 upgrade: adds the empty Storages map, only if absent.
// Deliberately does NOT call live code — migrations are frozen history.
// Migrated saves get no chest contents; the chest materializes on first open.
public sealed class MigrationV3ToV4 : ISaveMigration
{
    public int FromVersion => 3;

    public void Apply(JsonNode root)
    {
        if (root["Storages"] is null)
        {
            root["Storages"] = new JsonObject();
        }
    }
}
