using System.Text.Json;
using TheHaunt.Core;

namespace TheHaunt.Tests;

/// <summary>
/// The field-obstacle model: FarmActions' obstacle branch (strike, fell, break,
/// refusals) and ObstacleGen (seeded, spaced, respectful of occupied ground).
/// Pure Core — no scene tree; the view half lives in ObstacleViewTests.
/// </summary>
public static class ObstacleTests
{
    private const string MapId = "farm";

    private const int AxeSlot = 5;
    private const int PickSlot = 6;

    [SimTest]
    public static void Obstacle_AxeFellsATreeIntoAStump(TestContext t)
    {
        var data = TestKit.NewGameWithKit();
        MapState map = data.GetMap(MapId);
        map.Objects.Add(new PlacedObjectRecord { X = 4, Y = 4, ObjectId = ObstacleDefs.Tree });
        data.Player.Inventory.SelectedSlot = AxeSlot;

        ObstacleDef tree = ObstacleDefs.All[ObstacleDefs.Tree];
        for (int hit = 1; hit < tree.Hits; hit++)
        {
            t.AssertEqual(ActionOutcome.Struck,
                FarmActions.UseSelected(data, MapId, 4, 4, today: 10, terrainTillable: false),
                $"chop {hit} bites");
            t.AssertEqual(hit, map.GetObject(4, 4)!.HitsTaken, $"chop {hit} is remembered");
            t.AssertEqual(0, data.Player.Inventory.CountOf("lumber"), $"chop {hit} yields nothing yet");
        }
        t.AssertEqual(100 - (tree.Hits - 1) * 2, data.Player.Stamina,
            "every effective chop costs the axe's stamina");

        t.AssertEqual(ActionOutcome.Felled,
            FarmActions.UseSelected(data, MapId, 4, 4, today: 10, terrainTillable: false),
            "the last chop fells it");
        t.AssertEqual(tree.YieldCount, data.Player.Inventory.CountOf("lumber"), "the tree's lumber lands");
        PlacedObjectRecord stump = map.GetObject(4, 4)!;
        t.AssertEqual(ObstacleDefs.Stump, stump.ObjectId, "a stump stands where the tree did");
        t.AssertEqual(0, stump.HitsTaken, "the stump starts fresh");
    }

    [SimTest]
    public static void Obstacle_StumpAndRockBreakForYields(TestContext t)
    {
        var data = TestKit.NewGameWithKit();
        MapState map = data.GetMap(MapId);
        map.Objects.Add(new PlacedObjectRecord { X = 4, Y = 4, ObjectId = ObstacleDefs.Stump });
        map.Objects.Add(new PlacedObjectRecord { X = 8, Y = 4, ObjectId = ObstacleDefs.Rock });
        InventoryData inv = data.Player.Inventory;

        inv.SelectedSlot = AxeSlot;
        ObstacleDef stump = ObstacleDefs.All[ObstacleDefs.Stump];
        for (int hit = 1; hit < stump.Hits; hit++)
        {
            t.AssertEqual(ActionOutcome.Struck,
                FarmActions.UseSelected(data, MapId, 4, 4, today: 10, terrainTillable: false),
                $"stump chop {hit}");
        }
        t.AssertEqual(ActionOutcome.Broken,
            FarmActions.UseSelected(data, MapId, 4, 4, today: 10, terrainTillable: false),
            "the stump's last chop clears it");
        t.Assert(map.GetObject(4, 4) is null, "the stump is gone for good");
        t.AssertEqual(stump.YieldCount, inv.CountOf("lumber"), "and paid its extra lumber");

        inv.SelectedSlot = PickSlot;
        ObstacleDef rock = ObstacleDefs.All[ObstacleDefs.Rock];
        for (int hit = 1; hit < rock.Hits; hit++)
        {
            t.AssertEqual(ActionOutcome.Struck,
                FarmActions.UseSelected(data, MapId, 8, 4, today: 10, terrainTillable: false),
                $"rock strike {hit}");
        }
        t.AssertEqual(ActionOutcome.Broken,
            FarmActions.UseSelected(data, MapId, 8, 4, today: 10, terrainTillable: false),
            "the rock breaks");
        t.Assert(map.GetObject(8, 4) is null, "the rock is gone");
        t.AssertEqual(rock.YieldCount, inv.CountOf("stone"), "and paid its stone");
    }

    [SimTest]
    public static void Obstacle_WrongToolAndUnknownObjectsRefuse(TestContext t)
    {
        var data = TestKit.NewGameWithKit();
        MapState map = data.GetMap(MapId);
        map.Objects.Add(new PlacedObjectRecord { X = 4, Y = 4, ObjectId = ObstacleDefs.Tree });
        map.Objects.Add(new PlacedObjectRecord { X = 8, Y = 4, ObjectId = ObstacleDefs.Rock });
        map.Objects.Add(new PlacedObjectRecord { X = 12, Y = 4, ObjectId = "future.shrine" });
        InventoryData inv = data.Player.Inventory;

        // The cell's occupant owns the interaction: even over TILLABLE terrain the
        // hoe cannot till under a tree — the model guard, independent of the view.
        inv.SelectedSlot = 0;
        AssertRefusal(t, data, 4, 4, tillable: true, ActionOutcome.NoEffect, "hoe on a tree cell");

        inv.SelectedSlot = PickSlot;
        AssertRefusal(t, data, 4, 4, tillable: false, ActionOutcome.NoEffect, "pick on a tree");
        inv.SelectedSlot = AxeSlot;
        AssertRefusal(t, data, 8, 4, tillable: false, ActionOutcome.NoEffect, "axe on a rock");
        AssertRefusal(t, data, 12, 4, tillable: false, ActionOutcome.NoEffect,
            "axe on an object this build does not know (preserved, never struck)");

        inv.SelectedSlot = 1;
        AssertRefusal(t, data, 4, 4, tillable: false, ActionOutcome.NoEffect, "watering a tree");
    }

    [SimTest]
    public static void Obstacle_RefusalsLeaveTheModelUntouched(TestContext t)
    {
        var data = TestKit.NewGameWithKit();
        MapState map = data.GetMap(MapId);
        map.Objects.Add(new PlacedObjectRecord { X = 4, Y = 4, ObjectId = ObstacleDefs.Rock, HitsTaken = 2 });
        InventoryData inv = data.Player.Inventory;
        inv.SelectedSlot = PickSlot;

        // Not enough stamina: refuse BEFORE any mutation, even mid-way through a rock.
        data.Player.Stamina = 1;
        AssertRefusal(t, data, 4, 4, tillable: false, ActionOutcome.NotEnoughStamina,
            "a spent farmer cannot swing");
        data.Player.Stamina = 100;

        // Full inventory: the FINAL hit carries the yield, so it refuses whole —
        // no hit recorded, no stamina spent, nothing dropped on the floor.
        for (int slot = 0; slot < InventoryData.Capacity; slot++)
        {
            inv.Slots[slot] ??= new ItemStackRecord { ItemId = "turnip", Count = 99 };
        }
        inv.SelectedSlot = PickSlot;
        AssertRefusal(t, data, 4, 4, tillable: false, ActionOutcome.InventoryFull,
            "the breaking strike waits for pocket room");
    }

    [SimTest]
    public static void Obstacle_GenerationIsSeededAndSpaced(TestContext t)
    {
        List<(int X, int Y)> candidates = Grid(30, 30);
        var map = new MapState();

        List<PlacedObjectRecord> first = ObstacleGen.Generate(candidates, map, seed: 1234);
        List<PlacedObjectRecord> again = ObstacleGen.Generate(candidates, map, seed: 1234);
        t.AssertEqual(Layout(first), Layout(again), "the same seed grows the same field");

        List<PlacedObjectRecord> other = ObstacleGen.Generate(candidates, map, seed: 99);
        t.Assert(Layout(first) != Layout(other), "a different seed grows a different one");

        t.AssertEqual(ObstacleGen.TreeTarget, first.Count(o => o.ObjectId == ObstacleDefs.Tree),
            "a roomy field hits the tree target");
        t.AssertEqual(ObstacleGen.StumpTarget, first.Count(o => o.ObjectId == ObstacleDefs.Stump),
            "and the stump target");
        t.AssertEqual(ObstacleGen.RockTarget, first.Count(o => o.ObjectId == ObstacleDefs.Rock),
            "and the rock target");

        var cells = new HashSet<(int, int)>(candidates);
        foreach (PlacedObjectRecord obj in first)
        {
            t.Assert(cells.Contains((obj.X, obj.Y)), $"{obj.ObjectId} at ({obj.X},{obj.Y}) is on a candidate cell");
            t.AssertEqual(0, obj.HitsTaken, "generated obstacles start undamaged");
        }
        for (int i = 0; i < first.Count; i++)
        {
            for (int j = i + 1; j < first.Count; j++)
            {
                int distance = Math.Max(
                    Math.Abs(first[i].X - first[j].X), Math.Abs(first[i].Y - first[j].Y));
                t.Assert(distance > 1,
                    $"no two obstacles touch: ({first[i].X},{first[i].Y}) vs ({first[j].X},{first[j].Y})");
                if (first[i].ObjectId == ObstacleDefs.Tree && first[j].ObjectId == ObstacleDefs.Tree)
                {
                    t.Assert(distance >= ObstacleGen.TreeSpacing,
                        $"tree trunks keep their spacing: ({first[i].X},{first[i].Y}) vs ({first[j].X},{first[j].Y})");
                }
                // Canopies stay hollow: nothing may hide under a tree's 3x4 foliage
                // (an invisible wall the old hand layout deliberately avoided).
                foreach ((PlacedObjectRecord tree, PlacedObjectRecord under) in new[]
                         { (first[i], first[j]), (first[j], first[i]) })
                {
                    if (tree.ObjectId == ObstacleDefs.Tree)
                    {
                        t.Assert(Math.Abs(under.X - tree.X) > 1
                                || under.Y < tree.Y - 3 || under.Y > tree.Y,
                            $"{under.ObjectId} at ({under.X},{under.Y}) is not hidden under the tree at ({tree.X},{tree.Y})");
                    }
                }
            }
        }
    }

    [SimTest]
    public static void Obstacle_GenerationAvoidsWorkedAndOccupiedGround(TestContext t)
    {
        var map = new MapState();
        for (int x = 0; x < 30; x++)
        {
            map.SetTile(new TileRecord { X = x, Y = 3, Kind = "tilled" });   // an old save's plot
        }
        map.Objects.Add(new PlacedObjectRecord { X = 10, Y = 10, ObjectId = "chest" });

        List<PlacedObjectRecord> placed = ObstacleGen.Generate(Grid(30, 30), map, seed: 7);
        t.Assert(placed.Count > 0, "the rest of the field still grows");
        foreach (PlacedObjectRecord obj in placed)
        {
            t.Assert(obj.Y != 3, $"({obj.X},{obj.Y}) does not bury the tilled row");
            t.Assert(obj.X != 10 || obj.Y != 10, "nothing lands on the standing chest");
        }
    }

    private static List<(int X, int Y)> Grid(int width, int height)
    {
        var cells = new List<(int X, int Y)>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cells.Add((x, y));
            }
        }
        return cells;
    }

    private static string Layout(List<PlacedObjectRecord> objects) =>
        string.Join(";", objects.Select(o => $"{o.ObjectId}@{o.X},{o.Y}"));

    private static void AssertRefusal(TestContext t, GameData data, int x, int y,
        bool tillable, ActionOutcome expected, string label)
    {
        string before = Snapshot(data);
        ActionOutcome outcome = FarmActions.UseSelected(data, MapId, x, y, today: 10, tillable);
        t.AssertEqual(expected, outcome, $"{label}: outcome");
        t.AssertEqual(before, Snapshot(data), $"{label}: model untouched");
    }

    private static string Snapshot(GameData data) =>
        JsonSerializer.Serialize(data, SaveJsonContext.Default.GameData);
}
