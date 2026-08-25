using System.Text.Json.Nodes;

namespace TheHaunt.Core;

// Frozen v1 → v2 upgrade: grants the launch-era starter state as LITERAL JSON,
// each key only if absent. Deliberately does NOT call ItemDefs/StarterKit/NewGame —
// migrations are frozen history and must not drift when live content changes.
public sealed class MigrationV1ToV2 : ISaveMigration
{
    public int FromVersion => 1;

    public void Apply(JsonNode root)
    {
        if (root["Player"] is not JsonObject player)
        {
            player = new JsonObject();
            root["Player"] = player;
        }
        if (player["Money"] is null)
        {
            player["Money"] = 500;
        }
        if (player["Stamina"] is null)
        {
            player["Stamina"] = 100;
        }
        if (player["MaxStamina"] is null)
        {
            player["MaxStamina"] = 100;
        }
        if (player["Inventory"] is null)
        {
            player["Inventory"] = new JsonObject
            {
                ["Slots"] = new JsonArray(
                    Stack("hoe", 1),
                    Stack("watering_can", 1),
                    Stack("scythe", 1),
                    Stack("turnip_seeds", 15),
                    Stack("greenbean_seeds", 5),
                    null, null, null, null, null),
                ["SelectedSlot"] = 0,
            };
        }
        if (root["ShippingBin"] is null)
        {
            root["ShippingBin"] = new JsonArray();
        }
    }

    private static JsonObject Stack(string itemId, int count) =>
        new() { ["ItemId"] = itemId, ["Count"] = count };
}
