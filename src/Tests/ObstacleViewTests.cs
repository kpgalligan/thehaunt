using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// The view half of the field obstacles: TestMap paints MapState.Objects into
/// collision the hoe and the boot guard both respect, WorldSim's full tool path
/// refreshes it per swing, and generation runs once per save on real candidates.
/// </summary>
public static class ObstacleViewTests
{
    [SimTest]
    public static async Task Farm_ObstacleViewsPaintAndClear(TestContext t)
    {
        SaveService.Instance.NewGame();
        GameData data = SaveService.Instance.Current;
        MapState state = data.GetMap(MapIds.Farm);
        state.Objects.Add(new PlacedObjectRecord { X = 20, Y = 20, ObjectId = ObstacleDefs.Tree });
        state.Objects.Add(new PlacedObjectRecord { X = 24, Y = 20, ObjectId = ObstacleDefs.Rock });
        state.ObstaclesSeeded = true;   // hand-placed; nothing may generate over it

        var map = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        map.ApplyState(state);
        try
        {
            t.Assert(!map.IsStandable(new Vector2I(20, 20)), "the tree's trunk blocks");
            t.Assert(map.IsStandable(new Vector2I(19, 20)) && map.IsStandable(new Vector2I(21, 20)),
                "but the canopy is walked under");
            t.Assert(!map.IsTillable(20, 20), "and its cell refuses the hoe");
            t.Assert(!map.IsStandable(new Vector2I(24, 20)), "the rock blocks");
            t.Assert(!map.IsTillable(24, 20), "and refuses the hoe");

            // The whole path per swing: WorldSim -> FarmActions -> RefreshObstacle.
            InventoryData inv = data.Player.Inventory;
            inv.SelectedSlot = 5;   // axe (starter kit)
            ObstacleDef tree = ObstacleDefs.All[ObstacleDefs.Tree];
            ObstacleDef stump = ObstacleDefs.All[ObstacleDefs.Stump];
            for (int hit = 0; hit < tree.Hits; hit++)
            {
                WorldSim.Instance.UseSelectedItem(new Vector2I(20, 20));
            }
            t.AssertEqual(ObstacleDefs.Stump, state.GetObject(20, 20)!.ObjectId, "the tree fell to a stump");
            t.Assert(!map.IsTillable(20, 20), "the stump still owns the cell");
            for (int hit = 0; hit < stump.Hits; hit++)
            {
                WorldSim.Instance.UseSelectedItem(new Vector2I(20, 20));
            }
            t.Assert(state.GetObject(20, 20) is null, "the stump is cleared");
            t.Assert(map.IsTillable(20, 20), "and the ground under it takes the hoe again");
            t.Assert(map.IsStandable(new Vector2I(20, 20)), "and the feet");
            t.AssertEqual(tree.YieldCount + stump.YieldCount, inv.CountOf("lumber"),
                "the tree and its stump both paid lumber");

            // A hand-edited record on ground a build-time owner blocks (here: a
            // farmhouse facade cell) is preserved in the model but never drawn — so
            // the obstacle sync can neither paint over nor erase the facade's blocker.
            state.Objects.Add(new PlacedObjectRecord { X = 5, Y = 5, ObjectId = ObstacleDefs.Rock });
            map.ApplyState(state);
            t.Assert(!map.IsStandable(new Vector2I(5, 5)), "the facade cell still blocks");
            state.RemoveObject(5, 5);
            map.RefreshObstacle(5, 5, null);
            t.Assert(!map.IsStandable(new Vector2I(5, 5)),
                "and its blocker survives the record's removal — the sync never owned that cell");
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    [SimTest]
    public static async Task Farm_ObstacleCandidatesRespectTheMap(TestContext t)
    {
        SaveService.Instance.NewGame();
        var map = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        map.ApplyState(SaveService.Instance.Current.GetMap(MapIds.Farm));
        try
        {
            IReadOnlyList<Vector2I> candidates = map.ObstacleCandidates();
            t.Assert(candidates.Count > 200, $"the farm offers a real field ({candidates.Count} cells)");

            var reserved = new HashSet<Vector2I>(map.ReservedTiles());
            var spawns = new[] { new Vector2I(20, 15), new Vector2I(36, 24), new Vector2I(7, 8), new Vector2I(27, 10) };
            foreach (Vector2I cell in candidates)
            {
                t.Assert(map.IsTillable(cell.X, cell.Y),
                    $"candidate {cell} is open pasture (walkable, unreserved, unoccupied)");
                t.Assert(!reserved.Contains(cell), $"candidate {cell} is not reserved ground");
                t.Assert(!(cell.X is >= 4 and <= 15 && cell.Y is >= 23 and <= 26),
                    $"candidate {cell} stays out of the pen");
                foreach (Vector2I spawn in spawns)
                {
                    t.Assert(Math.Max(Math.Abs(cell.X - spawn.X), Math.Abs(cell.Y - spawn.Y)) > 1,
                        $"candidate {cell} keeps clear of spawn {spawn}");
                }
            }
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    [SimTest]
    public static async Task Farm_ObstaclesGenerateOncePerSave(TestContext t)
    {
        SaveService.Instance.NewGame();
        GameData data = SaveService.Instance.Current;
        var map = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            MapState state = data.GetMap(MapIds.Farm);
            t.Assert(!state.ObstaclesSeeded, "a new game starts unseeded");

            // A saved position mid-pasture and a scooter parked mid-pasture: both are
            // excluded ground ("a save must never wake up inside a rock"). Guaranteed
            // by the blocked set; a regression here fails per-seed, not always.
            data.Player.HasPosition = true;
            data.Player.X = 20 * 16 + 8;   // feet tile (20, 20)
            data.Player.Y = 20 * 16 + 2;
            data.Scooter.MapId = MapIds.Farm;
            data.Scooter.TileX = 28;
            data.Scooter.TileY = 12;
            data.Scooter.Mounted = false;

            WorldSim.Instance.EnsureObstacles(map);
            t.Assert(state.ObstaclesSeeded, "the first visit seeds the field");
            int grown = state.Objects.Count;
            t.Assert(grown > 0, $"and something grew ({grown} obstacles)");

            var candidates = new HashSet<Vector2I>(map.ObstacleCandidates());
            foreach (PlacedObjectRecord obj in state.Objects)
            {
                t.Assert(ObstacleDefs.TryGet(obj.ObjectId) is not null,
                    $"generated '{obj.ObjectId}' is a kind the defs know");
                t.Assert(candidates.Contains(new Vector2I(obj.X, obj.Y)),
                    $"{obj.ObjectId} at ({obj.X},{obj.Y}) grew on offered ground");
            }

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    t.Assert(state.GetObject(20 + dx, 20 + dy) is null,
                        $"the player's footing ring ({20 + dx},{20 + dy}) stays clear");
                }
            }
            t.Assert(state.GetObject(28, 12) is null, "the parked scooter's tile stays clear");

            foreach (NpcDef def in NpcDefs.All.Values)
            {
                foreach (ScheduleEntry entry in def.Schedule)
                {
                    if (entry.Placement.MapId == MapIds.Farm)
                    {
                        t.Assert(state.GetObject(entry.Placement.TileX, entry.Placement.TileY) is null,
                            $"NPC staging slot ({entry.Placement.TileX},{entry.Placement.TileY}) stays clear");
                    }
                }
            }

            WorldSim.Instance.EnsureObstacles(map);
            t.AssertEqual(grown, state.Objects.Count, "a second visit grows nothing new");

            // Cleared-out is not unseeded: an emptied field stays empty.
            state.Objects.Clear();
            WorldSim.Instance.EnsureObstacles(map);
            t.AssertEqual(0, state.Objects.Count, "a field cleared by hand stays cleared");
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }
}
