using System.Text.RegularExpressions;
using TheHaunt.Core;

namespace TheHaunt.Tests;

public static class InventoryTests
{
    [SimTest]
    public static void Items_DefsValidate(TestContext t)
    {
        foreach (ItemDef def in ItemDefs.All.Values)
        {
            t.Assert(Regex.IsMatch(def.IconColor, "^#[0-9a-fA-F]{6}$"),
                $"item '{def.Id}': IconColor '{def.IconColor}' parses as #rrggbb");
            if (def.Category == ItemCategory.Seed)
            {
                t.Assert(def.PlantsCropId != null, $"seed '{def.Id}' has a PlantsCropId");
                t.Assert(CropDefs.TryGet(def.PlantsCropId!) != null,
                    $"seed '{def.Id}': PlantsCropId '{def.PlantsCropId}' resolves");
            }
            if (def.Category == ItemCategory.Tool)
            {
                t.AssertEqual(1, def.MaxStack, $"tool '{def.Id}' MaxStack");
            }
        }

        foreach (CropDef crop in CropDefs.All.Values)
        {
            t.Assert(ItemDefs.TryGet(crop.HarvestItemId) != null,
                $"crop '{crop.Id}': HarvestItemId '{crop.HarvestItemId}' resolves");
            t.Assert(crop.StageDays.All(days => days >= 1), $"crop '{crop.Id}': all StageDays >= 1");
            if (crop.RegrowDays != 0)
            {
                t.Assert(crop.RegrowDays > 0 && crop.RegrowDays <= crop.TotalDays,
                    $"crop '{crop.Id}': 0 < RegrowDays ({crop.RegrowDays}) <= TotalDays ({crop.TotalDays})");
            }
        }

        var player = new PlayerData();
        StarterKit.Apply(player);
        foreach (ItemStackRecord? stack in player.Inventory.Slots)
        {
            if (stack == null)
            {
                continue;
            }
            t.Assert(ItemDefs.TryGet(stack.ItemId) != null, $"starter kit id '{stack.ItemId}' resolves");
        }
    }

    [SimTest]
    public static void Inventory_AddMergeOverflow(TestContext t)
    {
        var inv = new InventoryData();
        inv.Slots[2] = new ItemStackRecord { ItemId = "turnip", Count = 95 };
        inv.Slots[5] = new ItemStackRecord { ItemId = "turnip", Count = 90 };

        t.AssertEqual(0, inv.Add("turnip", 10), "merge add overflow");
        t.AssertEqual(99, inv.Slots[2]!.Count, "lowest-index stack topped up first");
        t.AssertEqual(96, inv.Slots[5]!.Count, "remainder tops up the next stack");
        t.Assert(inv.SlotAt(0) == null, "no empty slot used while existing stacks had room");

        t.AssertEqual(0, inv.Add("turnip", 5), "spill add overflow");
        t.AssertEqual(99, inv.Slots[5]!.Count, "second stack topped to MaxStack");
        AssertStack(t, inv.SlotAt(0), "turnip", 2, "spill starts a new stack in the lowest empty slot");

        var full = new InventoryData();
        t.AssertEqual(5, full.Add("turnip", 995), "overflow returned when full (10x99=990 fits)");
        for (int i = 0; i < InventoryData.Capacity; i++)
        {
            AssertStack(t, full.SlotAt(i), "turnip", 99, $"full inventory slot {i}");
        }
        t.AssertEqual(990, full.CountOf("turnip"), "MaxStack respected everywhere");
        t.AssertEqual(1, full.Add("turnip", 1), "completely full inventory places nothing");

        var tools = new InventoryData();
        t.AssertEqual(0, tools.Add("hoe", 3), "three hoes all placed");
        AssertStack(t, tools.SlotAt(0), "hoe", 1, "hoe slot 0");
        AssertStack(t, tools.SlotAt(1), "hoe", 1, "hoe slot 1");
        AssertStack(t, tools.SlotAt(2), "hoe", 1, "hoe slot 2");
        t.Assert(tools.Slots.Where(s => s != null).All(s => s!.Count == 1), "tools never stack");
    }

    [SimTest]
    public static void Inventory_NormalizeRepairs(TestContext t)
    {
        var inv = new InventoryData
        {
            Slots = new List<ItemStackRecord?>
            {
                new ItemStackRecord { ItemId = "turnip", Count = 250 },      // over-stack: kept intact
                new ItemStackRecord { ItemId = "mystery_relic", Count = 3 }, // unknown id: kept
                new ItemStackRecord { ItemId = "turnip", Count = 0 },        // dead stack: nulled
            },
            SelectedSlot = 42,
        };
        inv.Normalize();
        t.AssertEqual(InventoryData.Capacity, inv.Slots.Count, "short list padded to Capacity");
        AssertStack(t, inv.SlotAt(0), "turnip", 250, "over-stack kept intact");
        AssertStack(t, inv.SlotAt(1), "mystery_relic", 3, "unknown id kept");
        t.Assert(inv.SlotAt(2) == null, "count<=0 stack nulled");
        for (int i = 3; i < InventoryData.Capacity; i++)
        {
            t.Assert(inv.SlotAt(i) == null, $"padded slot {i} empty");
        }
        t.AssertEqual(InventoryData.Capacity - 1, inv.SelectedSlot, "SelectedSlot clamped down into range");

        var wide = new InventoryData { SelectedSlot = -3 };
        wide.Slots = new List<ItemStackRecord?>();
        for (int i = 0; i < 12; i++)
        {
            wide.Slots.Add(new ItemStackRecord { ItemId = "turnip", Count = 1 });
        }
        wide.Slots[3] = new ItemStackRecord { ItemId = "", Count = 5 };        // empty id: nulled
        wide.Slots[7] = new ItemStackRecord { ItemId = "turnip", Count = -2 }; // negative: nulled
        wide.Slots[11] = new ItemStackRecord { ItemId = "greenbean", Count = 7 };
        wide.Normalize();
        t.AssertEqual(12, wide.Slots.Count, "over-capacity list never trimmed (all 12 kept)");
        t.Assert(wide.Slots[3] == null, "empty item id nulled");
        t.Assert(wide.Slots[7] == null, "negative count nulled");
        AssertStack(t, wide.Slots[11], "greenbean", 7, "stack beyond Capacity kept");
        t.AssertEqual(0, wide.SelectedSlot, "negative SelectedSlot clamped up to 0");

        // Selection stays within the visible hotbar even when extra slots are preserved:
        // the clamp lands at Capacity - 1 (9), never Slots.Count - 1 (11).
        wide.SelectedSlot = 15;
        wide.Normalize();
        t.AssertEqual(InventoryData.Capacity - 1, wide.SelectedSlot,
            "over-capacity SelectedSlot clamped to Capacity-1, not Slots.Count-1");
    }

    private static void AssertStack(TestContext t, ItemStackRecord? stack, string itemId, int count, string label)
    {
        t.Assert(stack != null, $"{label}: stack present");
        t.AssertEqual(itemId, stack!.ItemId, $"{label}: item id");
        t.AssertEqual(count, stack.Count, $"{label}: count");
    }
}
