using System.Text.Json;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.Tests;

public static class EconTests
{
    [SimTest]
    public static void Econ_DepositMovesNotCopies(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            service.NewGame();
            TestKit.Fetch(service.Current); // kit in hand: the hoe below sits in slot 0
            InventoryData inv = service.Current.Player.Inventory;
            inv.Slots[5] = new ItemStackRecord { ItemId = "turnip", Count = 7 };
            inv.Slots[6] = new ItemStackRecord { ItemId = "turnip", Count = 4 };
            Dictionary<string, int> before = Totals(service.Current);

            WorldSim.Instance.SelectSlot(5);
            t.Assert(WorldSim.Instance.DepositSelectedToBin(), "deposit slot 5");
            t.Assert(service.Current.Player.Inventory.SlotAt(5) == null, "deposited slot nulled");
            WorldSim.Instance.SelectSlot(6);
            t.Assert(WorldSim.Instance.DepositSelectedToBin(), "deposit slot 6");

            List<ItemStackRecord> turnipStacks =
                service.Current.ShippingBin.Where(s => s.ItemId == "turnip").ToList();
            t.AssertEqual(1, turnipStacks.Count, "bin merges deposits by id");
            t.AssertEqual(11, turnipStacks[0].Count, "bin turnip count");
            AssertTotalsEqual(t, before, Totals(service.Current), "conservation after deposits");

            // Unsellable tool: refused, nothing moves.
            WorldSim.Instance.SelectSlot(0);
            t.Assert(!WorldSim.Instance.DepositSelectedToBin(), "tool deposit refused");
            t.AssertEqual("hoe", service.Current.Player.Inventory.SlotAt(0)?.ItemId, "hoe stays in slot 0");
            AssertTotalsEqual(t, before, Totals(service.Current), "conservation after refused deposit");

            // Empty slot: refused.
            WorldSim.Instance.SelectSlot(9);
            t.Assert(!WorldSim.Instance.DepositSelectedToBin(), "empty-slot deposit refused");

            service.DeserializeFrom(service.SerializeToString());
            AssertTotalsEqual(t, before, Totals(service.Current), "conservation after round-trip");
        }
        finally
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Econ_DepositOverflowRefused(TestContext t)
    {
        // Review pin: a bin merge near int.MaxValue must refuse (long math), never
        // wrap into a negative count the overnight sale would turn into negative money.
        SaveService service = SaveService.Instance;
        try
        {
            service.NewGame();
            service.Current.ShippingBin.Add(
                new ItemStackRecord { ItemId = "turnip", Count = int.MaxValue - 3 });
            InventoryData inv = service.Current.Player.Inventory;
            inv.Slots[5] = new ItemStackRecord { ItemId = "turnip", Count = 10 };
            WorldSim.Instance.SelectSlot(5);

            t.Assert(!WorldSim.Instance.DepositSelectedToBin(), "overflowing deposit refused");
            t.AssertEqual(10, inv.SlotAt(5)?.Count, "stack stays in the inventory");
            t.AssertEqual(int.MaxValue - 3, service.Current.ShippingBin[0].Count,
                "bin entry untouched");
        }
        finally
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Econ_ShippingOvernight(TestContext t)
    {
        SaveService service = SaveService.Instance;
        bool moneyFired = false;
        bool staminaFired = false;
        Action<long> onMoney = _ => moneyFired = true;
        Action<int, int> onStamina = (_, _) => staminaFired = true;
        WorldSim.Instance.MoneyChanged += onMoney;
        WorldSim.Instance.StaminaChanged += onStamina;
        string slotPath = Path.Combine(SaveService.SaveDirectory, "test_econ_ship.json");
        try
        {
            service.NewGame();
            InventoryData inv = service.Current.Player.Inventory;
            inv.Slots[5] = new ItemStackRecord { ItemId = "turnip", Count = 3 };
            inv.Slots[6] = new ItemStackRecord { ItemId = "greenbean", Count = 2 };
            WorldSim.Instance.SelectSlot(5);
            t.Assert(WorldSim.Instance.DepositSelectedToBin(), "deposit turnips");
            WorldSim.Instance.SelectSlot(6);
            t.Assert(WorldSim.Instance.DepositSelectedToBin(), "deposit greenbeans");

            // The pending shipment must survive a disk round-trip before it sells.
            t.Assert(service.Save("test_econ_ship"), "save with pending shipment");
            t.AssertEqual(LoadResult.Ok, service.Load("test_econ_ship"), "load the pending shipment back");
            t.AssertEqual(2, service.Current.ShippingBin.Count, "bin contents survived the round-trip");

            long moneyBefore = service.Current.Player.Money;
            long expected = 3L * ItemDefs.Get("turnip").SellPrice
                + 2L * ItemDefs.Get("greenbean").SellPrice;
            moneyFired = false;
            staminaFired = false;
            Clock.Instance.AdvanceToDayStart();

            t.AssertEqual(moneyBefore + expected, service.Current.Player.Money,
                "money += exact shipping sum");
            t.AssertEqual(0, service.Current.ShippingBin.Count, "bin emptied overnight");
            t.Assert(moneyFired, "MoneyChanged fired");
            t.Assert(staminaFired, "StaminaChanged fired");
        }
        finally
        {
            WorldSim.Instance.MoneyChanged -= onMoney;
            WorldSim.Instance.StaminaChanged -= onStamina;
            if (File.Exists(slotPath))
            {
                File.Delete(slotPath);
            }
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Shop_BuyHappyPath(TestContext t)
    {
        SaveService service = SaveService.Instance;
        var sequence = new List<string>();
        Action<long> onMoney = balance => sequence.Add($"money:{balance}");
        Action onInventory = () => sequence.Add("inventory");
        try
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            t.Assert(WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore), "shop session opened");
            WorldSim.Instance.MoneyChanged += onMoney;
            WorldSim.Instance.InventoryChanged += onInventory;

            long moneyBefore = service.Current.Player.Money;
            int seedsBefore = service.Current.Player.Inventory.CountOf("turnip_seeds");

            t.AssertEqual(BuyResult.Ok, WorldSim.Instance.BuyItem("turnip_seeds", 2),
                "buy 2 turnip seeds");
            t.AssertEqual(moneyBefore - 40L, service.Current.Player.Money, "exact debit (2 x 20g)");
            t.AssertEqual(seedsBefore + 2, service.Current.Player.Inventory.CountOf("turnip_seeds"),
                "stack added to the inventory");
            t.AssertEqual(2, sequence.Count, "exactly one MoneyChanged + one InventoryChanged");
            t.AssertEqual($"money:{moneyBefore - 40L}", sequence[0],
                "MoneyChanged first, carrying the new balance");
            t.AssertEqual("inventory", sequence[1], "InventoryChanged second");

            // The UI's Shift-buy is the same call with count 5 — all-or-nothing.
            sequence.Clear();
            t.AssertEqual(BuyResult.Ok, WorldSim.Instance.BuyItem("greenbean_seeds", 5),
                "buy 5 greenbean seeds");
            t.AssertEqual(moneyBefore - 40L - 300L, service.Current.Player.Money,
                "exact debit (5 x 60g)");
            t.AssertEqual(2, sequence.Count, "one event pair per buy");
        }
        finally
        {
            WorldSim.Instance.MoneyChanged -= onMoney;
            WorldSim.Instance.InventoryChanged -= onInventory;
            WorldSim.Instance.CloseShop();
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Shop_BuyFailuresMutateNothing(TestContext t)
    {
        SaveService service = SaveService.Instance;
        int events = 0;
        Action<long> onMoney = _ => events++;
        Action onInventory = () => events++;
        try
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            WorldSim.Instance.MoneyChanged += onMoney;
            WorldSim.Instance.InventoryChanged += onInventory;

            // No open session: UnknownItem, mutation-free.
            string before = Snapshot(service.Current);
            t.AssertEqual(BuyResult.UnknownItem, WorldSim.Instance.BuyItem("turnip_seeds", 1),
                "no session: UnknownItem");
            t.AssertEqual(before, Snapshot(service.Current), "no session: model bit-identical");

            t.Assert(WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore), "shop session opened");

            // Off-catalog item (turnip sells at the bin but is not for sale here).
            before = Snapshot(service.Current);
            t.AssertEqual(BuyResult.UnknownItem, WorldSim.Instance.BuyItem("turnip", 1),
                "off-catalog item: UnknownItem");
            t.AssertEqual(before, Snapshot(service.Current), "off-catalog: model bit-identical");

            // One gold short of a 20g seed packet.
            service.Current.Player.Money = 19;
            before = Snapshot(service.Current);
            t.AssertEqual(BuyResult.InsufficientFunds, WorldSim.Instance.BuyItem("turnip_seeds", 1),
                "19g < 20g: InsufficientFunds");
            t.AssertEqual(before, Snapshot(service.Current), "insufficient funds: model bit-identical");

            // No room: seeds stack topped to max and every slot occupied.
            service.Current.Player.Money = 10_000;
            TestKit.Fetch(service.Current); // kit in hand: slot 3 holds the starter seeds
            InventoryData inv = service.Current.Player.Inventory;
            inv.Slots[3]!.Count = 99; // starter turnip seeds -> max stack
            for (int i = 0; i < InventoryData.Capacity; i++)
            {
                inv.Slots[i] ??= new ItemStackRecord { ItemId = "turnip", Count = 99 };
            }
            before = Snapshot(service.Current);
            t.AssertEqual(BuyResult.NoRoom, WorldSim.Instance.BuyItem("turnip_seeds", 1),
                "full inventory: NoRoom");
            t.AssertEqual(before, Snapshot(service.Current), "no room: model bit-identical");

            t.AssertEqual(0, events, "no MoneyChanged/InventoryChanged fired by any failure");
        }
        finally
        {
            WorldSim.Instance.MoneyChanged -= onMoney;
            WorldSim.Instance.InventoryChanged -= onInventory;
            WorldSim.Instance.CloseShop();
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Shop_CatalogIdsResolve(TestContext t)
    {
        t.Assert(ShopCatalog.All.Count > 0, "catalog table non-empty");
        foreach ((string catalogId, IReadOnlyList<ShopEntry> entries) in ShopCatalog.All)
        {
            t.Assert(entries.Count > 0, $"catalog '{catalogId}' non-empty");
            foreach (ShopEntry entry in entries)
            {
                t.Assert(ItemDefs.TryGet(entry.ItemId) != null,
                    $"catalog '{catalogId}': item '{entry.ItemId}' resolves in ItemDefs");
                t.Assert(entry.BuyPrice > 0,
                    $"catalog '{catalogId}': item '{entry.ItemId}' has a positive buy price");
            }
        }
        t.Assert(ShopCatalog.TryGet(ShopCatalog.GeneralStore) != null,
            "general store catalog resolves");
        t.Assert(ShopCatalog.TryGet("no_such_catalog") == null,
            "unknown catalog id resolves to null");
    }

    [SimTest]
    public static void Shop_SeedResaleIsHalfBuy(TestContext t)
    {
        // Ratified pricing rule: every shop-sold seed resells (ships) for exactly
        // half its buy price — shipping leftover seeds is possible but always lossy,
        // so there is no buy-then-ship arbitrage. New catalog rows must keep this.
        foreach ((string catalogId, IReadOnlyList<ShopEntry> entries) in ShopCatalog.All)
        {
            foreach (ShopEntry entry in entries)
            {
                ItemDef def = ItemDefs.Get(entry.ItemId);
                t.AssertEqual(entry.BuyPrice, def.SellPrice * 2,
                    $"catalog '{catalogId}': '{entry.ItemId}' buy price is exactly 2x its sell price");
            }
        }
    }

    [SimTest]
    public static void Shop_HoursBoundary(TestContext t)
    {
        t.Assert(!ShopHours.IsOpen(179), "8:59 AM closed");
        t.Assert(ShopHours.IsOpen(180), "9:00 AM open (start-inclusive)");
        t.Assert(ShopHours.IsOpen(659), "4:59 PM still open");
        t.Assert(!ShopHours.IsOpen(660), "5:00 PM closed (end-exclusive)");

        // "Shop open" and "shopkeeper present" can never diverge: the schedule's
        // window IS the ShopHours constants, referenced directly.
        IReadOnlyList<ScheduleEntry> schedule = NpcDefs.All["shopkeeper"].Schedule;
        t.AssertEqual(1, schedule.Count, "shopkeeper schedule is a single window");
        t.AssertEqual(ShopHours.OpenMinute, schedule[0].StartMinuteOfDay,
            "schedule opens with the shop");
        t.AssertEqual(ShopHours.CloseMinute, schedule[0].EndMinuteOfDay,
            "schedule closes with the shop");
    }

    // Per-id totals across inventory + shipping bin: deposits must move items, never copy
    // or destroy them, so these totals are invariant through the whole deposit pipeline.
    private static Dictionary<string, int> Totals(GameData data)
    {
        var totals = new Dictionary<string, int>();
        foreach (ItemStackRecord? stack in data.Player.Inventory.Slots)
        {
            if (stack != null)
            {
                totals[stack.ItemId] = totals.GetValueOrDefault(stack.ItemId) + stack.Count;
            }
        }
        foreach (ItemStackRecord stack in data.ShippingBin)
        {
            totals[stack.ItemId] = totals.GetValueOrDefault(stack.ItemId) + stack.Count;
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

    // Whole-model snapshot: a refused buy must leave the save graph bit-identical.
    private static string Snapshot(GameData data) =>
        JsonSerializer.Serialize(data, SaveJsonContext.Default.GameData);
}
