using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.Tests;

public static class StorageTests
{
    [SimTest]
    public static void Storage_TransferConservesItems(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            service.NewGame();
            GameData data = service.Current;
            InventoryData inv = data.Player.Inventory;
            inv.Slots[5] = new ItemStackRecord { ItemId = "turnip", Count = 7 };
            inv.Slots[6] = new ItemStackRecord { ItemId = "greenbean", Count = 4 };
            Dictionary<string, int> before = Totals(data, StorageIds.FarmHouseChest);

            t.Assert(WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 5),
                "transfer slot 5 to the chest");
            t.Assert(inv.SlotAt(5) == null, "source slot vacated");
            StorageData chest = data.GetStorage(StorageIds.FarmHouseChest);
            t.AssertEqual(20, chest.Slots.Count, "chest lazily created at capacity");
            t.AssertEqual(7, StackOps.CountOf(chest.Slots, "turnip"), "whole stack arrived in the chest");
            AssertTotalsEqual(t, before, Totals(data, StorageIds.FarmHouseChest),
                "conservation after the deposit");

            t.Assert(WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 6),
                "transfer slot 6 to the chest");
            t.Assert(WorldSim.Instance.TransferToInventory(StorageIds.FarmHouseChest, 0),
                "transfer chest slot 0 back to the inventory");
            t.AssertEqual(7, inv.CountOf("turnip"), "turnips back in the inventory");
            t.AssertEqual(0, StackOps.CountOf(chest.Slots, "turnip"), "no turnips left in the chest");
            AssertTotalsEqual(t, before, Totals(data, StorageIds.FarmHouseChest),
                "conservation after the round trip");

            // Refusals move nothing: empty source slots on both sides.
            t.Assert(!WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 9),
                "empty inventory slot refused");
            t.Assert(!WorldSim.Instance.TransferToInventory(StorageIds.FarmHouseChest, 5),
                "empty chest slot refused");
            t.Assert(!WorldSim.Instance.TransferToInventory(StorageIds.FarmHouseChest, -1),
                "out-of-range chest slot refused");
            AssertTotalsEqual(t, before, Totals(data, StorageIds.FarmHouseChest),
                "conservation after the refusals");

            // Conservation must survive a serialize -> deserialize cycle too.
            service.DeserializeFrom(service.SerializeToString());
            AssertTotalsEqual(t, before, Totals(service.Current, StorageIds.FarmHouseChest),
                "conservation after a save round-trip");
        }
        finally
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Storage_PartialTransferOnFullDestination(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            service.NewGame();
            GameData data = service.Current;
            InventoryData inv = data.Player.Inventory;
            StorageData chest = data.GetStorage(StorageIds.FarmHouseChest);

            // Leave exactly 4 units of room in the chest: slot 0 at 95, the rest at max.
            chest.Slots[0] = new ItemStackRecord { ItemId = "turnip", Count = 95 };
            for (int i = 1; i < chest.Slots.Count; i++)
            {
                chest.Slots[i] = new ItemStackRecord { ItemId = "turnip", Count = 99 };
            }
            inv.Slots[5] = new ItemStackRecord { ItemId = "turnip", Count = 10 };
            Dictionary<string, int> before = Totals(data, StorageIds.FarmHouseChest);

            // What fits moves; the remainder stays in the SOURCE slot.
            t.Assert(WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 5),
                "partial transfer reports movement");
            t.AssertEqual(99, chest.Slots[0]!.Count, "destination stack topped to max");
            AssertStack(t, inv.SlotAt(5), "turnip", 6, "remainder stays in the source slot");
            AssertTotalsEqual(t, before, Totals(data, StorageIds.FarmHouseChest),
                "conservation through the partial transfer");

            // Destination now has zero room: false ONLY when nothing moved.
            t.Assert(!WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 5),
                "full destination refuses outright");
            AssertStack(t, inv.SlotAt(5), "turnip", 6, "refused stack untouched in its slot");

            // Mirror direction: pack the inventory so only 2 units of turnip room remain
            // (tool slots are full at max stack 1; seed stacks are a different id).
            inv.Slots[5]!.Count = 97;
            for (int i = 6; i < InventoryData.Capacity; i++)
            {
                inv.Slots[i] = new ItemStackRecord { ItemId = "turnip", Count = 99 };
            }
            before = Totals(data, StorageIds.FarmHouseChest);
            t.Assert(WorldSim.Instance.TransferToInventory(StorageIds.FarmHouseChest, 0),
                "partial withdraw reports movement");
            t.AssertEqual(99, inv.SlotAt(5)!.Count, "inventory stack topped to max");
            AssertStack(t, chest.Slots[0], "turnip", 97, "withdraw remainder stays in the chest slot");
            AssertTotalsEqual(t, before, Totals(data, StorageIds.FarmHouseChest),
                "conservation through the partial withdraw");

            t.Assert(!WorldSim.Instance.TransferToInventory(StorageIds.FarmHouseChest, 0),
                "full inventory refuses the withdraw outright");
            AssertStack(t, chest.Slots[0], "turnip", 97, "refused chest stack untouched");
        }
        finally
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Storage_UnknownItemTransfers(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            service.NewGame();
            GameData data = service.Current;
            InventoryData inv = data.Player.Inventory;
            StorageData chest = data.GetStorage(StorageIds.FarmHouseChest);

            // Unknown id into the chest: transfers normally at max stack 1.
            inv.Slots[5] = new ItemStackRecord { ItemId = "mystery_relic", Count = 1 };
            Dictionary<string, int> before = Totals(data, StorageIds.FarmHouseChest);
            t.Assert(WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 5),
                "unknown id deposits");
            t.Assert(inv.SlotAt(5) == null, "source slot vacated");
            AssertStack(t, chest.Slots[0], "mystery_relic", 1, "unknown id landed in the chest");

            // An unknown OVER-stack (hand-edited or future save) withdraws at max
            // stack 1: it spreads across empty slots — never destroyed, never merged.
            chest.Slots[3] = new ItemStackRecord { ItemId = "future.artifact", Count = 2 };
            before["future.artifact"] = 2;
            t.Assert(WorldSim.Instance.TransferToInventory(StorageIds.FarmHouseChest, 3),
                "unknown over-stack withdraws");
            t.Assert(chest.Slots[3] == null, "chest source slot vacated");
            t.AssertEqual(2, inv.CountOf("future.artifact"), "both units arrived in the inventory");
            AssertStack(t, inv.SlotAt(5), "future.artifact", 1, "first unit split at max stack 1");
            AssertStack(t, inv.SlotAt(6), "future.artifact", 1, "second unit split at max stack 1");

            // And the relic comes back out too — nothing is ever destroyed.
            t.Assert(WorldSim.Instance.TransferToInventory(StorageIds.FarmHouseChest, 0),
                "unknown relic withdraws");
            t.AssertEqual(1, inv.CountOf("mystery_relic"), "relic conserved through both directions");
            t.AssertEqual(0, StackOps.CountOf(chest.Slots, "mystery_relic"), "chest side emptied");
            AssertTotalsEqual(t, before, Totals(data, StorageIds.FarmHouseChest),
                "unknown-id totals conserved end to end");
        }
        finally
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Storage_TransferFiresEvents(TestContext t)
    {
        SaveService service = SaveService.Instance;
        var sequence = new List<string>();
        Action<string> onStorage = id => sequence.Add($"storage:{id}");
        Action onInventory = () => sequence.Add("inventory");
        try
        {
            service.NewGame();
            GameData data = service.Current;
            InventoryData inv = data.Player.Inventory;
            StorageData chest = data.GetStorage(StorageIds.FarmHouseChest);
            inv.Slots[5] = new ItemStackRecord { ItemId = "turnip", Count = 7 };
            WorldSim.Instance.StorageChanged += onStorage;
            WorldSim.Instance.InventoryChanged += onInventory;

            // Successful deposit: exactly one StorageChanged(id) THEN one InventoryChanged.
            t.Assert(WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 5),
                "deposit succeeds");
            t.AssertEqual(2, sequence.Count, "exactly two events per successful deposit");
            t.AssertEqual($"storage:{StorageIds.FarmHouseChest}", sequence[0],
                "StorageChanged first, with the storage id");
            t.AssertEqual("inventory", sequence[1], "InventoryChanged second");

            // Successful withdraw: same pair, same order.
            sequence.Clear();
            t.Assert(WorldSim.Instance.TransferToInventory(StorageIds.FarmHouseChest, 0),
                "withdraw succeeds");
            t.AssertEqual(2, sequence.Count, "exactly two events per successful withdraw");
            t.AssertEqual($"storage:{StorageIds.FarmHouseChest}", sequence[0],
                "StorageChanged first on withdraw");
            t.AssertEqual("inventory", sequence[1], "InventoryChanged second on withdraw");

            // Refusals fire NOTHING: empty sources, then a zero-room destination.
            sequence.Clear();
            t.Assert(!WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 9),
                "empty inventory slot refused");
            t.Assert(!WorldSim.Instance.TransferToInventory(StorageIds.FarmHouseChest, 1),
                "empty chest slot refused");
            for (int i = 0; i < chest.Slots.Count; i++)
            {
                chest.Slots[i] = new ItemStackRecord { ItemId = "turnip", Count = 99 };
            }
            inv.Slots[5] = new ItemStackRecord { ItemId = "turnip", Count = 1 };
            t.Assert(!WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 5),
                "full destination refused");
            t.AssertEqual(0, sequence.Count, "no events on any refusal");
        }
        finally
        {
            WorldSim.Instance.StorageChanged -= onStorage;
            WorldSim.Instance.InventoryChanged -= onInventory;
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Menu_SessionGates(TestContext t)
    {
        SaveService service = SaveService.Instance;
        int storageClosed = 0;
        int shopClosed = 0;
        Action onStorageClosed = () => storageClosed++;
        Action onShopClosed = () => shopClosed++;
        WorldSim.Instance.StorageClosed += onStorageClosed;
        WorldSim.Instance.ShopClosed += onShopClosed;
        try
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);

            // Without control both opens refuse (flag-gated via PlayerHasControl).
            GameState.Instance.TransitionTo(GameState.Phase.Dialogue);
            t.Assert(!WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest),
                "OpenStorage refused without control");
            t.Assert(!WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore),
                "OpenShop refused without control");
            t.Assert(WorldSim.Instance.OpenStorageId == null && WorldSim.Instance.OpenShopId == null,
                "no session leaked by the refusals");
            GameState.Instance.TransitionTo(GameState.Phase.Playing);

            // Unknown catalog ids never open a shop, even with control.
            t.Assert(!WorldSim.Instance.OpenShop("no_such_shop"), "unknown catalog id refused");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "refused open leaves the phase alone");

            // Storage session: Menu phase — clock frozen, player frozen, tree NOT paused.
            t.Assert(WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest),
                "OpenStorage accepted from Playing");
            t.AssertEqual(StorageIds.FarmHouseChest, WorldSim.Instance.OpenStorageId,
                "open storage id recorded");
            t.AssertEqual(GameState.Phase.Menu, GameState.Instance.Current,
                "storage session moved the phase to Menu");
            t.Assert(!GameState.Instance.ClockRuns, "clock frozen in Menu");
            t.Assert(!GameState.Instance.PlayerHasControl, "player frozen in Menu");
            t.Assert(!t.Tree.Paused, "tree NOT paused in Menu");

            // One session at a time (and Menu itself lacks control).
            t.Assert(!WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore),
                "shop refused while the chest is open");
            t.Assert(!WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest),
                "second chest open refused");

            WorldSim.Instance.CloseStorage();
            t.Assert(WorldSim.Instance.OpenStorageId == null, "storage session cleared on close");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "close restored Playing");
            t.AssertEqual(1, storageClosed, "StorageClosed fired once");
            WorldSim.Instance.CloseStorage();
            t.AssertEqual(1, storageClosed, "second close is a safe no-op");

            // Shop session mirrors the chest session exactly.
            t.Assert(WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore),
                "OpenShop accepted from Playing");
            t.AssertEqual(ShopCatalog.GeneralStore, WorldSim.Instance.OpenShopId,
                "open shop id recorded");
            t.AssertEqual(GameState.Phase.Menu, GameState.Instance.Current,
                "shop session moved the phase to Menu");
            t.Assert(!WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest),
                "chest refused while the shop is open");
            WorldSim.Instance.CloseShop();
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "shop close restored Playing");
            t.AssertEqual(1, shopClosed, "ShopClosed fired once");

            // AfterLoad mid-session force-closes and restores Playing — a load
            // discards the session's world (the dialogue-strand fix, applied to Menu).
            t.Assert(WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest),
                "chest reopened for the load test");
            t.AssertEqual(GameState.Phase.Menu, GameState.Instance.Current,
                "in Menu before the load");
            service.NewGame(); // fires AfterLoad
            t.Assert(WorldSim.Instance.OpenStorageId == null, "AfterLoad cleared the storage session");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "AfterLoad restored Playing");
            t.AssertEqual(2, storageClosed, "AfterLoad fired the missing StorageClosed");
        }
        finally
        {
            WorldSim.Instance.StorageClosed -= onStorageClosed;
            WorldSim.Instance.ShopClosed -= onShopClosed;
            WorldSim.Instance.CloseStorage();
            WorldSim.Instance.CloseShop();
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    // Per-id totals across the inventory + one storage: transfers must move items,
    // never copy or destroy them, so these totals are invariant through every
    // transfer, partial transfer, and refusal.
    private static Dictionary<string, int> Totals(GameData data, string storageId)
    {
        var totals = new Dictionary<string, int>();
        foreach (ItemStackRecord? stack in data.Player.Inventory.Slots)
        {
            if (stack != null)
            {
                totals[stack.ItemId] = totals.GetValueOrDefault(stack.ItemId) + stack.Count;
            }
        }
        foreach (ItemStackRecord? stack in data.GetStorage(storageId).Slots)
        {
            if (stack != null)
            {
                totals[stack.ItemId] = totals.GetValueOrDefault(stack.ItemId) + stack.Count;
            }
        }
        return totals;
    }

    private static void AssertTotalsEqual(TestContext t, Dictionary<string, int> expected,
        Dictionary<string, int> actual, string label)
    {
        t.AssertEqual(expected.Count, actual.Count, $"{label}: distinct item ids");
        foreach ((string id, int count) in expected)
        {
            t.AssertEqual(count, actual.GetValueOrDefault(id), $"{label}: total of '{id}'");
        }
    }

    private static void AssertStack(TestContext t, ItemStackRecord? stack, string itemId, int count, string label)
    {
        t.Assert(stack != null, $"{label}: stack present");
        t.AssertEqual(itemId, stack!.ItemId, $"{label}: item id");
        t.AssertEqual(count, stack.Count, $"{label}: count");
    }
}
