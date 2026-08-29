namespace TheHaunt.Core;

// Called ONLY by GameData.NewGame() — NEVER by save migrations, which grant the
// launch-era kit as frozen JSON literals into the player's INVENTORY (where the kit
// lived at launch; frozen history stays put).
public static class StarterKit
{
    // The previous owner's farewell letter promises the tools and seeds wait in the
    // barn, so a new game stocks them into the barn chest — finding them is the
    // player's first errand, not a spawn grant. Save_MigratedKitMatchesNewGame pins
    // these stacks against the migrations' inventory grants.
    public static void Apply(GameData data)
    {
        StorageData chest = data.GetStorage(StorageIds.BarnChest);
        chest.Slots[0] = new ItemStackRecord { ItemId = "hoe", Count = 1 };
        chest.Slots[1] = new ItemStackRecord { ItemId = "watering_can", Count = 1 };
        chest.Slots[2] = new ItemStackRecord { ItemId = "scythe", Count = 1 };
        chest.Slots[3] = new ItemStackRecord { ItemId = "turnip_seeds", Count = 15 };
        chest.Slots[4] = new ItemStackRecord { ItemId = "greenbean_seeds", Count = 5 };
        // Basic-tier axe and pick are "inherited with the farm" (tools handoff).
        chest.Slots[5] = new ItemStackRecord { ItemId = "axe", Count = 1 };
        chest.Slots[6] = new ItemStackRecord { ItemId = "pick", Count = 1 };
    }
}
