namespace TheHaunt.Core;

// Called ONLY by GameData.NewGame() — NEVER by save migrations, which grant the
// launch-era kit as frozen JSON literals instead.
public static class StarterKit
{
    public static void Apply(PlayerData player)
    {
        var inventory = player.Inventory;
        inventory.Slots[0] = new ItemStackRecord { ItemId = "hoe", Count = 1 };
        inventory.Slots[1] = new ItemStackRecord { ItemId = "watering_can", Count = 1 };
        inventory.Slots[2] = new ItemStackRecord { ItemId = "scythe", Count = 1 };
        inventory.Slots[3] = new ItemStackRecord { ItemId = "turnip_seeds", Count = 15 };
        inventory.Slots[4] = new ItemStackRecord { ItemId = "greenbean_seeds", Count = 5 };
        // Basic-tier axe and pick are "inherited with the farm" (tools handoff).
        // MigrationV4ToV5 grants these same slots to older saves.
        inventory.Slots[5] = new ItemStackRecord { ItemId = "axe", Count = 1 };
        inventory.Slots[6] = new ItemStackRecord { ItemId = "pick", Count = 1 };
        inventory.SelectedSlot = 0;
    }
}
