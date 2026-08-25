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
}
