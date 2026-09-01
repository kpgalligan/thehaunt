using TheHaunt.Core;

namespace TheHaunt.Tests;

/// <summary>
/// A new game stocks the starter kit into the barn chest (StarterKit) — the player
/// fetches it as their first errand. Tests that need the kit IN HAND fetch it here,
/// chest slot i -> inventory slot i, so the launch slot layout (0 hoe, 1 watering can,
/// 2 scythe, 3 turnip seeds, 4 greenbean seeds, 5 axe, 6 pick) keeps holding wherever
/// a test selects by index. Call only on a fresh game's empty inventory.
/// </summary>
public static class TestKit
{
    public static GameData NewGameWithKit()
    {
        GameData data = GameData.NewGame();
        Fetch(data);
        return data;
    }

    public static void Fetch(GameData data)
    {
        StorageData chest = data.GetStorage(StorageIds.BarnChest);
        List<ItemStackRecord?> slots = data.Player.Inventory.Slots;
        for (int i = 0; i < chest.Slots.Count && i < slots.Count; i++)
        {
            if (chest.Slots[i] is { } stack)
            {
                slots[i] = stack;
                chest.Slots[i] = null;
            }
        }
    }
}
