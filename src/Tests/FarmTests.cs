using System.Text.Json;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.Tests;

public static class FarmTests
{
    private const string MapId = "farm";

    // Starter-kit slot layout (see StarterKit.Apply): 0 hoe, 1 watering can,
    // 2 scythe, 3 turnip seeds, 4 greenbean seeds.
    private const int HoeSlot = 0;
    private const int CanSlot = 1;
    private const int ScytheSlot = 2;
    private const int TurnipSeedSlot = 3;

    [SimTest]
    public static void Farm_Transitions(TestContext t)
    {
        var data = GameData.NewGame();
        InventoryData inv = data.Player.Inventory;
        MapState map = data.GetMap(MapId); // pre-create so refusal snapshots are stable

        // Hoe.
        inv.SelectedSlot = HoeSlot;
        AssertRefusal(t, data, 0, 0, today: 10, tillable: false,
            ActionOutcome.InvalidTarget, "hoe on non-tillable terrain");
        t.AssertEqual(ActionOutcome.Tilled,
            FarmActions.UseSelected(data, MapId, 1, 1, 10, true), "hoe on virgin tillable");
        TileRecord tile = map.GetTile(1, 1)!;
        t.AssertEqual("tilled", tile.Kind, "tilled kind");
        t.Assert(tile.CropId == null, "tilled tile has no crop");
        t.AssertEqual(-1L, tile.LastWateredDay, "tilled tile starts unwatered");
        t.AssertEqual(98, data.Player.Stamina, "hoe stamina cost deducted");
        AssertRefusal(t, data, 1, 1, 10, true, ActionOutcome.NoEffect, "hoe on an existing record");

        // Watering can.
        inv.SelectedSlot = CanSlot;
        AssertRefusal(t, data, 2, 2, 10, true, ActionOutcome.NoEffect, "can on absent tile");
        t.AssertEqual(ActionOutcome.Watered,
            FarmActions.UseSelected(data, MapId, 1, 1, 10, true), "can on tilled");
        t.AssertEqual(10L, tile.LastWateredDay, "watered day recorded");
        t.AssertEqual(97, data.Player.Stamina, "can stamina cost deducted");

        // Seeds.
        inv.SelectedSlot = TurnipSeedSlot;
        AssertRefusal(t, data, 3, 3, 10, true, ActionOutcome.NoEffect, "seed on absent tile");
        t.AssertEqual(ActionOutcome.Planted,
            FarmActions.UseSelected(data, MapId, 1, 1, 10, true), "seed on tilled");
        t.AssertEqual("turnip", tile.CropId, "planted crop id");
        t.AssertEqual(0, tile.GrowthDay, "planted growth day");
        t.AssertEqual(10L, tile.LastWateredDay, "planting PRESERVES LastWateredDay");
        t.AssertEqual(14, inv.CountOf("turnip_seeds"), "one seed consumed");
        t.AssertEqual(97, data.Player.Stamina, "seeds cost no stamina");
        AssertRefusal(t, data, 1, 1, 10, true, ActionOutcome.NoEffect, "seed on already-planted tile");

        // Can on planted.
        inv.SelectedSlot = CanSlot;
        t.AssertEqual(ActionOutcome.Watered,
            FarmActions.UseSelected(data, MapId, 1, 1, 11, true), "can on planted");
        t.AssertEqual(11L, tile.LastWateredDay, "re-watered day recorded");

        // Scythe.
        inv.SelectedSlot = ScytheSlot;
        AssertRefusal(t, data, 4, 4, 11, true, ActionOutcome.NoEffect, "scythe on absent tile");
        inv.SelectedSlot = HoeSlot;
        t.AssertEqual(ActionOutcome.Tilled,
            FarmActions.UseSelected(data, MapId, 5, 5, 11, true), "second till");
        inv.SelectedSlot = ScytheSlot;
        AssertRefusal(t, data, 5, 5, 11, true, ActionOutcome.NoEffect, "scythe on empty tilled tile");
        t.AssertEqual(ActionOutcome.Cleared,
            FarmActions.UseSelected(data, MapId, 1, 1, 11, true), "scythe on planted non-mature");
        t.Assert(tile.CropId == null, "cleared crop removed");
        t.AssertEqual(0, tile.GrowthDay, "cleared growth reset");
        t.AssertEqual("tilled", tile.Kind, "cleared tile stays tilled");
        t.AssertEqual(11L, tile.LastWateredDay, "clearing keeps LastWateredDay");
        t.AssertEqual(0, inv.CountOf("turnip"), "scythe yields nothing");

        // Mature intercept: harvest wins regardless of the selected item, at zero stamina.
        map.SetTile(new TileRecord
        {
            X = 6, Y = 6, Kind = "tilled", CropId = "turnip", GrowthDay = 5, LastWateredDay = 3,
        });
        inv.SelectedSlot = TurnipSeedSlot;
        int staminaBefore = data.Player.Stamina;
        t.AssertEqual(ActionOutcome.Harvested,
            FarmActions.UseSelected(data, MapId, 6, 6, 11, true), "seed on mature harvests");
        TileRecord matureTile = map.GetTile(6, 6)!;
        t.Assert(matureTile.CropId == null, "harvested single-harvest crop removed");
        t.AssertEqual(0, matureTile.GrowthDay, "harvested growth reset");
        t.AssertEqual(1, inv.CountOf("turnip"), "harvest yield added");
        t.AssertEqual(14, inv.CountOf("turnip_seeds"), "no seed consumed by the intercept");
        t.AssertEqual(staminaBefore, data.Player.Stamina, "harvest costs no stamina");

        // Scythe vs mature: the harvest intercept must win over the scythe's clear.
        map.SetTile(new TileRecord
        {
            X = 7, Y = 7, Kind = "tilled", CropId = "turnip", GrowthDay = 5, LastWateredDay = 3,
        });
        inv.SelectedSlot = ScytheSlot;
        int turnipsBefore = inv.CountOf("turnip");
        staminaBefore = data.Player.Stamina;
        t.AssertEqual(ActionOutcome.Harvested,
            FarmActions.UseSelected(data, MapId, 7, 7, 11, true), "scythe on mature harvests, not clears");
        TileRecord scythed = map.GetTile(7, 7)!;
        t.Assert(scythed.CropId == null, "scythe-harvested single-harvest crop removed");
        t.AssertEqual(0, scythed.GrowthDay, "scythe-harvested growth reset");
        t.AssertEqual(turnipsBefore + 1, inv.CountOf("turnip"), "scythe harvest yields exactly 1 turnip");
        t.AssertEqual(staminaBefore, data.Player.Stamina,
            "harvest is free — Cleared would have charged the scythe's cost of 1");

        // Unknown crop id: the scythe preserves it, same rule as unknown items.
        map.SetTile(new TileRecord
        {
            X = 8, Y = 8, Kind = "tilled", CropId = "mystery_vine", GrowthDay = 2, LastWateredDay = 3,
        });
        AssertRefusal(t, data, 8, 8, 11, true, ActionOutcome.NoEffect, "scythe on unknown crop id");
        TileRecord mystery = map.GetTile(8, 8)!;
        t.AssertEqual("mystery_vine", mystery.CropId, "unknown crop id preserved");
        t.AssertEqual(2, mystery.GrowthDay, "unknown crop growth unchanged");
        t.AssertEqual(staminaBefore, data.Player.Stamina, "scythe on unknown crop charges nothing");
    }

    [SimTest]
    public static void Farm_StageForDayTable(TestContext t)
    {
        // Pin the exact day -> stage tables as independent hardcoded values, so a
        // StageForDay regression cannot hide behind tests that recompute via StageForDay.
        CropDef turnip = CropDefs.Get("turnip");        // StageDays {1,1,1,2}, TotalDays 5
        int[] turnipStages = { 0, 1, 2, 3, 3, 4 };
        for (int day = 0; day <= 5; day++)
        {
            t.AssertEqual(turnipStages[day], turnip.StageForDay(day), $"turnip StageForDay({day})");
        }
        t.AssertEqual(turnip.StageDays.Length, turnip.StageForDay(turnip.TotalDays + 1),
            "turnip StageForDay clamps past mature");

        CropDef greenbean = CropDefs.Get("greenbean");  // StageDays {1,1,2,2}, TotalDays 6
        int[] greenbeanStages = { 0, 1, 2, 2, 3, 3, 4 };
        for (int day = 0; day <= 6; day++)
        {
            t.AssertEqual(greenbeanStages[day], greenbean.StageForDay(day), $"greenbean StageForDay({day})");
        }
        t.AssertEqual(greenbean.StageDays.Length, greenbean.StageForDay(greenbean.TotalDays + 1),
            "greenbean StageForDay clamps past mature");
    }

    [SimTest]
    public static void Farm_OvernightGrowthExact(TestContext t)
    {
        var data = GameData.NewGame();
        MapState map = data.GetMap(MapId);
        var tile = new TileRecord
        {
            X = 1, Y = 1, Kind = "tilled", CropId = "turnip", GrowthDay = 0, LastWateredDay = 5,
        };
        map.SetTile(tile);

        OvernightReport report = OvernightSim.Run(data, 5);
        t.AssertEqual(1, tile.GrowthDay, "watered day 5, Run(5) grows to 1");
        t.AssertEqual(1, report.CropsGrown, "report counts the grown crop");

        OvernightSim.Run(data, 6);
        t.AssertEqual(1, tile.GrowthDay, "unwatered night does not grow");

        data.Player.Inventory.SelectedSlot = CanSlot;
        t.AssertEqual(ActionOutcome.Watered,
            FarmActions.UseSelected(data, MapId, 1, 1, 7, true), "first watering");
        t.AssertEqual(ActionOutcome.Watered,
            FarmActions.UseSelected(data, MapId, 1, 1, 7, true), "second watering same day");
        OvernightSim.Run(data, 7);
        t.AssertEqual(2, tile.GrowthDay, "double-watered day grows exactly once");

        // Growth survives a serialize -> deserialize -> Run cycle.
        var fresh = GameData.NewGame();
        fresh.GetMap(MapId).SetTile(new TileRecord
        {
            X = 2, Y = 2, Kind = "tilled", CropId = "turnip", GrowthDay = 0, LastWateredDay = 3,
        });
        string json = JsonSerializer.Serialize(fresh, SaveJsonContext.Default.GameData);
        GameData reloaded = JsonSerializer.Deserialize(json, SaveJsonContext.Default.GameData)!;
        foreach (MapState m in reloaded.Maps.Values)
        {
            m.RebuildIndex();
        }
        OvernightSim.Run(reloaded, 3);
        t.AssertEqual(1, reloaded.GetMap(MapId).GetTile(2, 2)!.GrowthDay,
            "round-tripped crop grows exactly 1");
    }

    [SimTest]
    public static void Overnight_ReportItemizesSales(TestContext t)
    {
        var data = GameData.NewGame();
        data.ShippingBin.Add(new ItemStackRecord { ItemId = "turnip", Count = 5 });
        data.ShippingBin.Add(new ItemStackRecord { ItemId = "greenbean", Count = 2 });
        data.ShippingBin.Add(new ItemStackRecord { ItemId = "mystery_relic", Count = 3 });
        long moneyBefore = data.Player.Money;

        OvernightReport report = OvernightSim.Run(data, dayEnding: 0);

        // One ShippedLine per SOLD stack, in deposit order; the unknown id sells
        // nothing and produces no line.
        t.Assert(report.Sales != null, "report carries the itemized sales");
        IReadOnlyList<ShippedLine> sales = report.Sales!;
        t.AssertEqual(2, sales.Count, "one line per sold stack, none for the unknown id");
        t.AssertEqual(new ShippedLine("turnip", 5, 200), sales[0], "turnip line (5 x 40)");
        t.AssertEqual(new ShippedLine("greenbean", 2, 80), sales[1], "greenbean line (2 x 40)");

        long lineSum = 0;
        foreach (ShippedLine line in sales)
        {
            lineSum += line.Proceeds;
        }
        t.AssertEqual(report.ShippingProceeds, lineSum, "line proceeds sum to ShippingProceeds");
        t.AssertEqual(280L, report.ShippingProceeds, "proceeds exact");
        t.AssertEqual(moneyBefore + 280L, data.Player.Money, "money credited by the same sum");

        // Unknown/unsellable ids stay binned — item deletion is data loss.
        t.AssertEqual(1, data.ShippingBin.Count, "unknown id stays binned");
        t.AssertEqual("mystery_relic", data.ShippingBin[0].ItemId, "the surviving stack is the unknown id");
        t.AssertEqual(3, data.ShippingBin[0].Count, "unknown stack count preserved");
    }

    [SimTest]
    public static void Overnight_NonPositiveBinEntryNeverSells(TestContext t)
    {
        // Review pin: a hostile-writer Count <= 0 bin entry must be skipped and
        // preserved — selling it would MINT negative money and brick the save on
        // the next load's negative-Money check.
        var data = GameData.NewGame();
        data.ShippingBin.Add(new ItemStackRecord { ItemId = "turnip", Count = -5 });
        long moneyBefore = data.Player.Money;

        OvernightReport report = OvernightSim.Run(data, dayEnding: 0);

        t.AssertEqual(0L, report.ShippingProceeds, "no proceeds from a degenerate entry");
        t.AssertEqual(0, report.Sales!.Count, "no ShippedLine for a degenerate entry");
        t.AssertEqual(moneyBefore, data.Player.Money, "money untouched");
        t.AssertEqual(1, data.ShippingBin.Count, "entry preserved for the load repair to drop");
    }

    [SimTest]
    public static void Farm_RegrowCycle(TestContext t)
    {
        var data = GameData.NewGame();
        data.Player.Inventory.SelectedSlot = HoeSlot; // harvest intercept ignores selection
        MapState map = data.GetMap(MapId);
        var tile = new TileRecord
        {
            X = 2, Y = 2, Kind = "tilled", CropId = "greenbean", GrowthDay = 0, LastWateredDay = -1,
        };
        map.SetTile(tile);
        CropDef def = CropDefs.Get("greenbean");
        t.AssertEqual(6, def.TotalDays, "greenbean TotalDays");
        t.AssertEqual(3, def.RegrowDays, "greenbean RegrowDays");

        for (long day = 0; day < def.TotalDays; day++)
        {
            tile.LastWateredDay = day;
            OvernightSim.Run(data, day);
            t.Assert(tile.GrowthDay <= def.TotalDays, "GrowthDay never exceeds TotalDays");
        }
        t.AssertEqual(def.TotalDays, tile.GrowthDay, "mature after 6 watered nights");

        t.AssertEqual(ActionOutcome.Harvested,
            FarmActions.UseSelected(data, MapId, 2, 2, def.TotalDays, true), "first harvest");
        t.AssertEqual(def.TotalDays - def.RegrowDays, tile.GrowthDay,
            "regrow crop rewinds to TotalDays - RegrowDays");
        t.AssertEqual("greenbean", tile.CropId, "regrow crop stays planted");
        t.AssertEqual(1, data.Player.Inventory.CountOf("greenbean"), "first harvest yield");

        for (long day = 10; day < 10 + def.RegrowDays; day++)
        {
            tile.LastWateredDay = day;
            OvernightSim.Run(data, day);
            t.Assert(tile.GrowthDay <= def.TotalDays, "GrowthDay never exceeds TotalDays during regrow");
        }
        t.AssertEqual(def.TotalDays, tile.GrowthDay, "mature again after 3 more watered nights");

        tile.LastWateredDay = 20;
        OvernightSim.Run(data, 20);
        t.AssertEqual(def.TotalDays, tile.GrowthDay, "watered mature crop never exceeds TotalDays");

        t.AssertEqual(ActionOutcome.Harvested,
            FarmActions.UseSelected(data, MapId, 2, 2, 21, true), "second harvest");
        t.AssertEqual(2, data.Player.Inventory.CountOf("greenbean"), "second harvest yield");
    }

    [SimTest]
    public static void Farm_HarvestFullInventoryRefuses(TestContext t)
    {
        var data = GameData.NewGame();
        InventoryData inv = data.Player.Inventory;
        for (int i = 5; i < InventoryData.Capacity; i++)
        {
            inv.Slots[i] = new ItemStackRecord { ItemId = "turnip", Count = 99 };
        }
        t.Assert(!inv.HasRoomFor("turnip", 1), "inventory reads as full for turnips");

        MapState map = data.GetMap(MapId);
        map.SetTile(new TileRecord
        {
            X = 1, Y = 1, Kind = "tilled", CropId = "turnip", GrowthDay = 5, LastWateredDay = 2,
        });
        inv.SelectedSlot = HoeSlot;

        string before = Snapshot(data);
        t.AssertEqual(ActionOutcome.InventoryFull,
            FarmActions.UseSelected(data, MapId, 1, 1, 6, true), "full inventory refuses harvest");
        t.AssertEqual(before, Snapshot(data), "refused harvest leaves the model bit-identical");

        inv.Slots[9] = null;
        int countBefore = inv.CountOf("turnip");
        t.AssertEqual(ActionOutcome.Harvested,
            FarmActions.UseSelected(data, MapId, 1, 1, 6, true), "harvest after freeing a slot");
        t.AssertEqual(countBefore + CropDefs.Get("turnip").HarvestCount, inv.CountOf("turnip"),
            "exactly HarvestCount added once");
        t.Assert(map.GetTile(1, 1)!.CropId == null, "single-harvest crop removed");
    }

    [SimTest]
    public static void Stamina_RefuseAndRestore(TestContext t)
    {
        var data = GameData.NewGame();
        data.Player.Stamina = 1; // below the hoe's cost of 2
        data.Player.Inventory.SelectedSlot = HoeSlot;
        MapState map = data.GetMap(MapId); // pre-create so the refusal snapshot is stable

        string before = Snapshot(data);
        t.AssertEqual(ActionOutcome.NotEnoughStamina,
            FarmActions.UseSelected(data, MapId, 1, 1, 0, true), "hoe refused below cost");
        t.AssertEqual(before, Snapshot(data), "refusal mutates nothing and deducts nothing");
        t.Assert(map.GetTile(1, 1) == null, "no tile record created");

        OvernightSim.Run(data, 0);
        t.AssertEqual(data.Player.MaxStamina, data.Player.Stamina, "overnight restores stamina to max");
    }

    [SimTest]
    public static void Farm_UnloadedMapGrows(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            service.NewGame(); // clock back to day 0
            MapState never = service.Current.GetMap("never_instanced");
            never.SetTile(new TileRecord
            {
                X = 2, Y = 3, Kind = "tilled", CropId = "turnip",
                GrowthDay = 0, LastWateredDay = Clock.Instance.Now.DayIndex,
            });

            Clock.Instance.AdvanceToDayStart();
            t.AssertEqual(1, never.GetTile(2, 3)!.GrowthDay,
                "crop on a never-instanced map grew overnight");
        }
        finally
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    private static void AssertRefusal(TestContext t, GameData data, int x, int y, long today,
        bool tillable, ActionOutcome expected, string label)
    {
        string before = Snapshot(data);
        ActionOutcome outcome = FarmActions.UseSelected(data, MapId, x, y, today, tillable);
        t.AssertEqual(expected, outcome, $"{label}: outcome");
        t.AssertEqual(before, Snapshot(data), $"{label}: model bit-identical");
    }

    private static string Snapshot(GameData data) =>
        JsonSerializer.Serialize(data, SaveJsonContext.Default.GameData);
}
