using System.Text.Json.Nodes;

namespace TheHaunt.Core;

// Frozen v4 → v5 upgrade: grants the axe and pickaxe (tools handoff — the basic
// tier is "inherited with the farm"), each only if absent anywhere in the
// inventory. Prefers the tool's starter-kit slot (axe 5, pick 6) so a migrated
// launch-era save stays slot-for-slot identical to a new game; falls back to the
// first free slot, and skips the grant outright when the inventory is full.
// Deliberately does NOT call ItemDefs/StarterKit — migrations are frozen history.
public sealed class MigrationV4ToV5 : ISaveMigration
{
    public int FromVersion => 4;

    public void Apply(JsonNode root)
    {
        if (root["Player"] is not JsonObject player
            || player["Inventory"] is not JsonObject inventory
            || inventory["Slots"] is not JsonArray slots)
        {
            return; // no inventory to grant into; load repair owns degenerate saves
        }
        Grant(slots, "axe", preferredSlot: 5);
        Grant(slots, "pick", preferredSlot: 6);
    }

    private static void Grant(JsonArray slots, string itemId, int preferredSlot)
    {
        foreach (JsonNode? slot in slots)
        {
            if (slot is JsonObject stack && stack["ItemId"] is JsonValue id
                && id.TryGetValue(out string? existing) && existing == itemId)
            {
                return;
            }
        }
        int free = preferredSlot < slots.Count && slots[preferredSlot] is null
            ? preferredSlot
            : FirstFree(slots);
        if (free >= 0)
        {
            slots[free] = new JsonObject { ["ItemId"] = itemId, ["Count"] = 1 };
        }
    }

    private static int FirstFree(JsonArray slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] is null)
            {
                return i;
            }
        }
        return -1;
    }
}
