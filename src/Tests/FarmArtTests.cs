using System.Reflection;
using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// Guards the wiring between the farm/interiors art
/// (docs/designs/design_handoff_farm_interiors) and the geometry the player actually
/// runs into. Same division as the town's art tests: "looks right" is a screenshot's
/// job, "walks right" and "reads the model correctly" are these.
/// </summary>
public static class FarmArtTests
{
    [SimTest]
    public static void FarmTerrain_MergesTheWoodsEdgeAndDerivesWalkable(TestContext t)
    {
        TileSet tileSet = FarmTerrain.Get();
        t.AssertEqual(2, tileSet.GetSourceCount(),
            "the farm sheet plus a private copy of the town atlas");

        var farm = (TileSetAtlasSource)tileSet.GetSource(FarmTerrain.FarmSource);
        var town = (TileSetAtlasSource)tileSet.GetSource(FarmTerrain.TownSource);
        t.AssertEqual(64, farm.GetTilesCount(), "the farm sheet's 64 tiles");
        t.Assert(town.GetTilesCount() > 50, "the town atlas came across whole");

        foreach (TileSetAtlasSource source in new[] { farm, town })
        {
            for (int i = 0; i < source.GetTilesCount(); i++)
            {
                Vector2I coords = source.GetTileId(i);
                TileData data = source.GetTileData(coords, 0);
                t.Assert(
                    data.GetCustomData(TileSetTools.WalkableData).AsBool()
                        != data.GetCollisionPolygonsCount(0) > 0,
                    $"tile {coords}: walkable is derived from collision, never hand-listed");
            }
        }

        // The farm sheet has no transparent cell of its own, so the blocker the Obstacles
        // layer paints under every sprite is the town atlas's.
        t.Assert(!town.GetTileData(TerrainTiles.Blocker, 0)
            .GetCustomData(TileSetTools.WalkableData).AsBool(), "the blocker cell is unwalkable");

        foreach (Vector2I solid in new[]
                 {
                     FarmTiles.RockLarge, FarmTiles.Stump, FarmTiles.Log,
                     FarmTiles.FenceH, FarmTiles.FenceV, FarmTiles.GateClosed,
                     FarmTiles.FenceCornerSe, FarmTiles.FenceCornerNw,
                 })
        {
            t.Assert(!farm.GetTileData(solid, 0).GetCustomData(TileSetTools.WalkableData).AsBool(),
                $"farm tile {solid} blocks");
        }
        foreach (Vector2I open in new[]
                 {
                     FarmTiles.GateOpen, FarmTiles.RockSmall, FarmTiles.HayScatter,
                     FarmTiles.Path[0], FarmTiles.Pasture[0], FarmTiles.SoilDry[0],
                 })
        {
            t.Assert(farm.GetTileData(open, 0).GetCustomData(TileSetTools.WalkableData).AsBool(),
                $"farm tile {open} is passable");
        }
    }

    [SimTest]
    public static void FarmSoil_AutotilesFromTheNeighboursAndFurrowsTheInterior(TestContext t)
    {
        // The soil sets use the town dirt set's column order, so both index through one
        // table. Every configuration must land on its own column of the right row.
        var seen = new HashSet<Vector2I>();
        for (int mask = 0; mask < 16; mask++)
        {
            Vector2I dry = FarmTiles.SoilEdge(false,
                (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0);
            Vector2I wet = FarmTiles.SoilEdge(true,
                (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0);
            t.AssertEqual(1, dry.Y, $"grass mask {mask}: the dry set is row 1");
            t.AssertEqual(2, wet.Y, $"grass mask {mask}: the wet set is row 2");
            t.AssertEqual(dry.X, wet.X, $"grass mask {mask}: same column, different row");
            t.Assert(seen.Add(dry), $"grass mask {mask}: distinct column ({dry.X})");
        }
        t.AssertEqual(new Vector2I(9, 1), FarmTiles.SoilEdge(false, false, false, false, false),
            "no grass on any side is the plot interior");
        t.AssertEqual(new Vector2I(0, 2), FarmTiles.SoilEdge(true, true, true, true, true),
            "grass all round is the isolated wet cell");

        t.AssertEqual(FarmTiles.FurrowDryH, FarmTiles.Furrow(false, true, true, true, true)!.Value,
            "a cell with soil all round is hoed east-west");
        t.AssertEqual(FarmTiles.FurrowWetV, FarmTiles.Furrow(true, true, false, true, false)!.Value,
            "a north-south run is hoed north-south");
        t.Assert(FarmTiles.Furrow(false, true, true, false, false) == null,
            "an elbow has no direction to read");

        // The cell-state function, end to end.
        var lone = new TileRecord { X = 5, Y = 5, Kind = "tilled", LastWateredDay = -1 };
        var (soil, crop) = TestMap.CellState(lone, 3, false, false, false, false);
        t.AssertEqual(new Vector2I(0, 1), soil!.Value, "a freshly hoed lone cell is dry and isolated");
        t.Assert(crop == null, "nothing planted yet");

        lone.LastWateredDay = 3;
        (soil, _) = TestMap.CellState(lone, 3, false, false, false, false);
        t.AssertEqual(new Vector2I(0, 2), soil!.Value, "watered today reads wet");
        (soil, _) = TestMap.CellState(lone, 4, false, false, false, false);
        t.AssertEqual(new Vector2I(0, 1), soil!.Value, "and dry again the next morning");

        (soil, _) = TestMap.CellState(lone, 4, true, true, true, true);
        t.AssertEqual(FarmTiles.FurrowDryH, soil!.Value, "surrounded by soil, it furrows");

        t.Assert(TestMap.CellState(null, 0, false, false, false, false) is (null, null),
            "no record paints nothing");

        // An id the crop sheet has no row for is preserved by the model and simply not
        // drawn — never a crash, never a destroyed record.
        var unknown = new TileRecord { Kind = "tilled", CropId = "mystery_vine", GrowthDay = 2 };
        (soil, crop) = TestMap.CellState(unknown, 0, false, false, false, false);
        t.Assert(soil != null && crop == null, "unknown crop id: soil still paints, crop does not");
    }

    [SimTest]
    public static void Crops_AddressedByCropOrderAndStage(TestContext t)
    {
        // The sheet's physical row order, pinned literally rather than re-derived from
        // CropDefs — the handoff drew turnip, greenbean, potato, cauliflower in that
        // order, so reordering the catalog swaps what every planted crop looks like and
        // this is the only thing that would notice.
        t.AssertEqual(0, CropTiles.RowByCropId["turnip"], "turnip row");
        t.AssertEqual(1, CropTiles.RowByCropId["greenbean"], "greenbean row");
        t.AssertEqual(2, CropTiles.RowByCropId["potato"], "potato row");
        t.AssertEqual(3, CropTiles.RowByCropId["cauliflower"], "cauliflower row");
        t.AssertEqual(CropDefs.All.Count, CropTiles.RowByCropId.Count,
            "every crop in the catalog has a row");

        CropDef turnip = CropDefs.Get("turnip");
        t.AssertEqual(new Vector2I(0, 0), CropTiles.Cell("turnip", 0)!.Value, "turnip, day 0");
        t.AssertEqual(new Vector2I(turnip.StageDays.Length, 0),
            CropTiles.Cell("turnip", turnip.TotalDays)!.Value,
            "the mature column is StageDays.Length — column 4 for every shipped crop");
        t.AssertEqual(new Vector2I(turnip.StageDays.Length, 0),
            CropTiles.Cell("turnip", turnip.TotalDays + 50)!.Value,
            "and it stays there however long it stands");

        t.Assert(CropTiles.Cell("mystery_vine", 1) == null, "an unknown id draws nothing");

        var source = (TileSetAtlasSource)CropTiles.Get().GetSource(0);
        t.AssertEqual(20, source.GetTilesCount(), "4 crops x 5 stages");
        t.AssertEqual(new Vector2I(16, 32), source.TextureRegionSize,
            "cells are 16x32: beans climb and cauliflower spreads (handoff §3)");
        t.AssertEqual(new Vector2I(0, 8), source.GetTileData(Vector2I.Zero, 0).TextureOrigin,
            "the origin lifts the cell so a crop stands on its tile and overhangs the row above");
        // POSITIVE, against the handoff's stated (0,-8): Godot SUBTRACTS texture_origin
        // from the draw position, so a negative Y sinks the cell instead of lifting it.
        // Measured: at (0,-8) a mature turnip on cell (2,2) drew at y 32..64 — a whole
        // tile below its own row; at (0,8) it draws at y 16..48, feet on the cell's
        // bottom row. See the correction note in the farm handoff README §3.
    }

    [SimTest]
    public static async Task FarmSoil_TillingRepaintsItsNeighbours(TestContext t)
    {
        // The soil set is an autotile, so the "O(1) incremental update" is a five-cell
        // plus: hoeing a cell also re-solves the edge of everything orthogonally next to
        // it. Without that, a plot grows a grass seam down the middle that only a map
        // reload repairs.
        SaveService.Instance.NewGame();
        TestKit.Fetch(SaveService.Instance.Current); // hoe in hand, not in the barn chest
        var map = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            var west = new Vector2I(18, 20);
            var east = new Vector2I(19, 20);
            var soil = map.GetNodeOrNull<TileMapLayer>("FarmSoil");
            t.Assert(soil != null, "the FarmSoil layer exists");

            WorldSim.Instance.SelectSlot(0);   // hoe
            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(west), "west tilled");
            t.AssertEqual(new Vector2I(0, 1), soil!.GetCellAtlasCoords(west),
                "alone, it takes the grass-all-round column");

            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(east), "east tilled");
            t.AssertEqual(FarmTiles.SoilEdge(false, true, false, true, true),
                soil.GetCellAtlasCoords(west),
                "the ALREADY-PAINTED west cell lost its eastern grass edge");
            t.AssertEqual(FarmTiles.SoilEdge(false, true, true, true, false),
                soil.GetCellAtlasCoords(east), "and the new cell keeps grass on its other three");

            // The full repaint from the model must agree with the incremental one.
            Vector2I incremental = soil.GetCellAtlasCoords(west);
            map.ApplyState(SaveService.Instance.Current.GetMap(MapIds.Farm));
            t.AssertEqual(incremental, soil.GetCellAtlasCoords(west),
                "rebuild equals incremental for a cell with a worked neighbour");
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    [SimTest]
    public static async Task ShippingBin_LidFollowsTheModel(TestContext t)
    {
        SaveService.Instance.NewGame();
        var map = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            var bin = map.GetNodeOrNull<ShippingBin>("Interactables/ShippingBin");
            t.Assert(bin != null, "the farm has a shipping bin");
            Sprite2D? lid = null;
            foreach (Node child in bin!.GetChildren())
                if (child is Sprite2D sprite)
                    lid = sprite;
            t.Assert(lid != null, "and it draws itself from the sheet");
            t.AssertEqual(FarmBuildings.BinClosed, lid!.RegionRect, "shut on an empty morning");

            InventoryData inventory = SaveService.Instance.Current.Player.Inventory;
            inventory.Add("turnip", 3);
            int slot = -1;
            for (int i = 0; i < InventoryData.Capacity; i++)
                if (inventory.SlotAt(i)?.ItemId == "turnip")
                    slot = i;
            t.Assert(slot >= 0, "the turnips landed in a slot");
            WorldSim.Instance.SelectSlot(slot);
            t.Assert(WorldSim.Instance.DepositSelectedToBin(), "the turnips go in");
            t.AssertEqual(FarmBuildings.BinOpen, lid.RegionRect,
                "the lid stands open while there is produce in it");

            Clock.Instance.AdvanceToDayStart();
            t.AssertEqual(0, SaveService.Instance.Current.ShippingBin.Count, "the night sold them");
            t.AssertEqual(FarmBuildings.BinClosed, lid.RegionRect, "and the lid comes back down");
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    [SimTest]
    public static async Task Barn_TheDrawnStateFollowsTheFlags(TestContext t)
    {
        SaveService.Instance.NewGame();
        var farm = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(farm);
        MapRoot barn = MapRegistry.Create(MapIds.Barn);
        t.Host.AddChild(barn);
        await t.WaitFrames(1);
        try
        {
            var facade = farm.GetNodeOrNull<BarnFacade>("Barn");
            var dressing = barn.GetNodeOrNull<TileMapLayer>("Dressing");
            var floor = barn.GetNodeOrNull<TileMapLayer>("Ground");
            t.Assert(facade != null && dressing != null && floor != null,
                "the barn is a facade in the yard and a room behind it");

            farm.ApplyState(SaveService.Instance.Current.GetMap(MapIds.Farm));
            t.AssertEqual(BarnFacade.Variant(BarnRules.Derelict), facade!.RegionRect,
                "it came with the farm and it is falling down");
            t.Assert(dressing!.GetCellSourceId(new Vector2I(1, 1)) != -1,
                "cobwebs hang in the corner of the derelict barn");
            t.AssertEqual(InteriorTiles.FloorStain, floor!.GetCellAtlasCoords(new Vector2I(6, 2)),
                "and the floor under the ladder is stained");

            // One flag, and both the yard and the room repaint through the bus.
            t.Assert(WorldSim.Instance.SetStoryFlag(StoryKeys.BarnRestored), "stamp barn_restored");
            t.AssertEqual(BarnFacade.Variant(BarnRules.Restored), facade.RegionRect,
                "the yard sees barn red");
            t.AssertEqual(-1, dressing.GetCellSourceId(new Vector2I(1, 1)), "the webs come down");
            t.AssertEqual(InteriorTiles.FloorDirt, floor.GetCellAtlasCoords(new Vector2I(6, 2)),
                "and the stains are swept");
        }
        finally
        {
            barn.Free();
            farm.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    [SimTest]
    public static async Task Farm_GeometryMatchesTheArt(TestContext t)
    {
        SaveService.Instance.NewGame();
        var map = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        map.ApplyState(SaveService.Instance.Current.GetMap(MapIds.Farm));

        try
        {
            // The two door cells are the only openings in the two facades.
            t.Assert(!map.IsStandable(new Vector2I(7, 7)), "farmhouse door cell carries the Door blocker");
            t.Assert(!map.IsStandable(new Vector2I(27, 9)), "barn door cell carries the Door blocker");
            t.Assert(map.IsStandable(new Vector2I(7, 8)), "farmhouse door approach is walkable");
            t.Assert(map.IsStandable(new Vector2I(27, 10)) && map.IsStandable(new Vector2I(28, 10)),
                "the barn's approach is two tiles wide, under its drawn double door");

            foreach (Vector2I wall in new[]
                     { new Vector2I(4, 4), new Vector2I(9, 7), new Vector2I(25, 5), new Vector2I(30, 9) })
            {
                t.Assert(!map.IsStandable(wall), $"building footprint {wall} blocks");
            }
            // The overhang above a facade is walked BEHIND, not through.
            t.Assert(map.IsStandable(new Vector2I(6, 3)), "the farmhouse roof overhang stays walkable");
            t.Assert(map.IsStandable(new Vector2I(27, 4)), "the barn roof overhang stays walkable");

            // Trees are save-state obstacles now (ObstacleGen); only a trunk cell is
            // solid — the player walks under the branches. ObstacleViewTests goes deep;
            // this keeps the trunk/canopy rule visible beside the rest of the geometry.
            MapState farmState = SaveService.Instance.Current.GetMap(MapIds.Farm);
            farmState.Objects.Add(new PlacedObjectRecord { X = 3, Y = 12, ObjectId = ObstacleDefs.Tree });
            map.ApplyState(farmState);
            t.Assert(!map.IsStandable(new Vector2I(3, 12)), "a tree trunk blocks");
            t.Assert(map.IsStandable(new Vector2I(2, 12)) && map.IsStandable(new Vector2I(4, 12)),
                "the canopy either side of it does not");

            // The map limit is woods, and it opens only where the road leaves for town.
            foreach (Vector2I edge in new[]
                     { new Vector2I(0, 0), new Vector2I(39, 29), new Vector2I(20, 1), new Vector2I(1, 20) })
            {
                t.Assert(!map.IsStandable(edge), $"woods border {edge} blocks");
            }
            t.Assert(map.IsStandable(new Vector2I(36, 28)) && map.IsStandable(new Vector2I(37, 29)),
                "the south road mouth stays open for the town exit");
            t.Assert(!map.IsStandable(new Vector2I(39, 14)) && !map.IsStandable(new Vector2I(39, 15)),
                "the old east mouth is sealed woods again");

            // The storm blockade is debris, and it is there until the road clears.
            t.Assert(!map.IsStandable(new Vector2I(36, 26)) && !map.IsStandable(new Vector2I(37, 27)),
                "fallen timber and rock still close the road");
            WorldSim.Instance.SetStoryFlag(StoryKeys.RoadCleared);
            t.Assert(map.IsStandable(new Vector2I(36, 26)) && map.IsStandable(new Vector2I(37, 27)),
                "and the crew hauls it away");

            // The pen is solid rails with one way in.
            t.Assert(!map.IsStandable(new Vector2I(8, 26)), "a pen rail blocks");
            t.Assert(!map.IsStandable(new Vector2I(4, 23)), "a pen corner blocks");
            t.Assert(map.IsStandable(new Vector2I(9, 26)), "the gate is drawn and painted open");

            // Row 27 is the one clear band the map-swap stress test hoes across.
            for (int x = 3; x <= 32; x++)
                t.Assert(map.IsTillable(x, 27), $"({x},27) is open pasture");

            // The drawn shipping bin is two tiles wide now, so both its cells are
            // reserved — tilling under a sprite would render invisibly.
            t.Assert(!map.IsTillable(10, 8) && !map.IsTillable(11, 8),
                "both of the bin's cells refuse the hoe");
            t.Assert(map.IsTillable(9, 8), "the cell beside it does not");

            // A sprite covers ground the player can still walk on. Soil hoed under a roof
            // would be painted and never seen again, so those cells refuse the hoe while
            // staying walkable.
            foreach (Vector2I hidden in new[]
                     { new Vector2I(4, 2), new Vector2I(9, 3), new Vector2I(25, 3), new Vector2I(30, 4) })
            {
                t.Assert(map.IsStandable(hidden), $"{hidden} is walked behind, not blocked");
                t.Assert(!map.IsTillable(hidden.X, hidden.Y), $"{hidden} is under a roof and refuses the hoe");
            }
            t.Assert(!map.IsTillable(9, 26), "the pen's gateway is the one cell of it that would take soil");

            // Hiding the blockade sign has to take its collider with it — a StaticBody2D
            // under a hidden parent keeps colliding, and (35,26) sits beside the road
            // the player rides on every trip to town.
            await t.WaitFrames(1);
            var sign = map.GetNodeOrNull<Sign>("Interactables/BlockadeSign");
            t.Assert(sign != null && !sign.Visible, "the sign went with the debris");
            foreach (Node child in sign!.GetChildren())
            {
                if (child is StaticBody2D blocker)
                    t.AssertEqual(0u, blocker.CollisionLayer, "and left no invisible wall behind");
            }

            // Dressing never lands on a tile the intro stages an NPC on.
            foreach (Vector2I staging in new[]
                     { new Vector2I(33, 15), new Vector2I(34, 14), new Vector2I(32, 16) })
            {
                t.Assert(map.IsStandable(staging), $"crew staging tile {staging} is clear");
            }

            foreach (string spawn in new[] { "default", "road", "house_door", "barn_door" })
            {
                Vector2 position = map.GetSpawn(spawn);
                var tile = new Vector2I(
                    Mathf.FloorToInt(position.X / MapRoot.TileSize),
                    Mathf.FloorToInt((position.Y + 6) / MapRoot.TileSize));
                t.Assert(map.IsStandable(tile), $"spawn '{spawn}' at {tile} is standable");
            }

            t.Assert(!map.IsInterior, "the farm takes the day/night tint");
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    [SimTest]
    public static void InteriorAtlas_WalkableMatchesCollision(TestContext t)
    {
        TileSet tileSet = InteriorTerrain.Get();
        t.AssertEqual(2, tileSet.GetSourceCount(),
            "the sheet plus the transparent blocker every cell of it is too opaque to be");

        var sheet = (TileSetAtlasSource)tileSet.GetSource(InteriorTerrain.TileSource);
        t.AssertEqual(64, sheet.GetTilesCount(), "all 64 interior tiles");

        int walkable = 0;
        for (int i = 0; i < sheet.GetTilesCount(); i++)
        {
            Vector2I coords = sheet.GetTileId(i);
            TileData data = sheet.GetTileData(coords, 0);
            bool passable = data.GetCustomData(TileSetTools.WalkableData).AsBool();
            t.Assert(passable != data.GetCollisionPolygonsCount(0) > 0,
                $"tile {coords}: walkable and collision agree");
            if (passable)
                walkable++;
        }
        // Row 0's sixteen floors, plus the two openings in the solid rows.
        t.AssertEqual(18, walkable, "walkable tile count");
        foreach (Vector2I open in new[] { InteriorTiles.DoorOpen, InteriorTiles.Cobweb })
        {
            t.Assert(sheet.GetTileData(open, 0).GetCustomData(TileSetTools.WalkableData).AsBool(),
                $"{open} is one of the two passable non-floor tiles");
        }

        var blockers = (TileSetAtlasSource)tileSet.GetSource(InteriorTerrain.BlockerSource);
        TileData blocker = blockers.GetTileData(InteriorTerrain.Blocker, 0);
        t.Assert(!blocker.GetCustomData(TileSetTools.WalkableData).AsBool()
            && blocker.GetCollisionPolygonsCount(0) == 1,
            "the furniture blocker blocks and draws nothing");
    }

    [SimTest]
    public static async Task Interiors_ShellHoldsTheGameplayCoordinates(TestContext t)
    {
        SaveService.Instance.NewGame();
        // Target map + spawn are pinned here because IsStandable alone can no longer see
        // the Door node: the wall ring paints door_open on Obstacles, which makes the
        // cell unstandable on its own. Without this, a room could lose its way out
        // entirely and every geometry assertion would still pass.
        var rooms = new (string MapId, Vector2I Door, Vector2I Threshold, Vector2I Inside,
            string Target, string Spawn)[]
        {
            (MapIds.FarmHouse, new(7, 9), new(7, 8), new(7, 6), MapIds.Farm, "house_door"),
            (MapIds.GeneralStore, new(7, 9), new(7, 8), new(7, 6), MapIds.Town, "from_store"),
            (MapIds.TownHall, new(20, 22), new(20, 21), new(20, 19), MapIds.Town, "from_hall"),
            (MapIds.Barn, new(8, 11), new(8, 10), new(7, 7), MapIds.Farm, "barn_door"),
            (MapIds.Motel, new(7, 9), new(7, 8), new(7, 6), MapIds.WestEntry, "from_motel"),
            (MapIds.GasStation, new(5, 8), new(5, 7), new(5, 5), MapIds.WestEntry, "from_gas"),
            (MapIds.BilliesBar, new(7, 11), new(7, 10), new(7, 7), MapIds.Billies, "from_bar"),
            (MapIds.Salon, new(6, 8), new(6, 7), new(6, 5), MapIds.EastEntry, "from_salon"),
            (MapIds.MotelRoom1, new(4, 6), new(4, 5), new(4, 3), MapIds.WestEntry, "from_room1"),
            (MapIds.MotelRoom2, new(4, 6), new(4, 5), new(4, 3), MapIds.WestEntry, "from_room2"),
            (MapIds.MotelRoom3, new(4, 6), new(4, 5), new(4, 3), MapIds.WestEntry, "from_room3"),
            (MapIds.MotelRoom4, new(4, 6), new(4, 5), new(4, 3), MapIds.WestEntry, "from_room4"),
        };

        foreach (var (mapId, door, threshold, inside, target, spawn) in rooms)
        {
            MapRoot map = MapRegistry.Create(mapId);
            t.Host.AddChild(map);
            await t.WaitFrames(1);
            try
            {
                t.Assert(map.IsInterior, $"'{mapId}' takes the fixed warm key, not the tint");
                t.Assert(!map.IsStandable(door), $"'{mapId}': the door cell carries its blocker");
                t.Assert(map.IsStandable(threshold),
                    $"'{mapId}': the threshold just inside the door is walkable");
                t.Assert(map.IsStandable(inside), $"'{mapId}': the room's middle is walkable");
                t.Assert(!map.IsStandable(new Vector2I(0, 0)),
                    $"'{mapId}': the wall ring blocks at its corner");
                t.Assert(!map.IsStandable(new Vector2I(door.X, 0)),
                    $"'{mapId}': the cornice row blocks");

                Door? way = FindDoor(map);
                t.Assert(way != null, $"'{mapId}' has a way out");
                t.AssertEqual(door, TileOf(way!.GlobalPosition),
                    $"'{mapId}': the Door node sits on the drawn doorway");
                t.AssertEqual(target, way.TargetMapId, $"'{mapId}': door target map");
                t.AssertEqual(spawn, way.TargetSpawnId, $"'{mapId}': door target spawn");
                t.Assert(!way.DrawPlaceholder,
                    $"'{mapId}': the doorway is drawn into the wall, so the node draws nothing");

                Rect2 limits = map.GetCameraLimits();
                t.Assert(limits.Size.X >= MapRoot.ViewportWidth && limits.Size.Y >= MapRoot.ViewportHeight,
                    $"'{mapId}': camera limits cover the viewport");
            }
            finally
            {
                map.Free();
                await t.WaitFrames(1);
            }
        }

        // The store's back room stays sealed by the counter, so the shopkeeper's
        // scheduled cell is unreachable by construction rather than by convention.
        var store = MapRegistry.Create(MapIds.GeneralStore);
        t.Host.AddChild(store);
        await t.WaitFrames(1);
        try
        {
            for (int x = 1; x <= 12; x++)
                t.Assert(!store.IsStandable(new Vector2I(x, 4)), $"counter cell ({x},4) blocks");
            t.Assert(!store.IsStandable(new Vector2I(0, 4)) && !store.IsStandable(new Vector2I(13, 4)),
                "and it meets the wall on both sides");
            t.Assert(store.IsStandable(new Vector2I(6, 3)),
                "the shopkeeper's scheduled cell behind it is clear of the back-room stock");
        }
        finally
        {
            store.Free();
            await t.WaitFrames(1);
        }

        // The mayor's staging cell is the row in front of the hall's long table.
        var hall = MapRegistry.Create(MapIds.TownHall);
        t.Host.AddChild(hall);
        await t.WaitFrames(1);
        try
        {
            t.Assert(hall.IsStandable(new Vector2I(20, 6)), "the mayor's cell (20,6) is clear");
            t.Assert(!hall.IsStandable(new Vector2I(20, 5)), "the long table behind it is not");
            foreach (Vector2I seat in new[] { new Vector2I(18, 12), new Vector2I(20, 12), new Vector2I(22, 12) })
                t.Assert(hall.IsStandable(seat), $"crew staging tile {seat} is clear of the pews");
        }
        finally
        {
            hall.Free();
            await t.WaitFrames(1);
        }
    }

    [SimTest]
    public static async Task Interiors_EveryOpenCellIsReachable(TestContext t)
    {
        // Furniture blockers are laid cell by cell, so it is easy to wall a corner off by
        // accident. Flood-fill from the door and account for every walkable cell: the
        // store's back room is sealed on purpose (the counter runs wall to wall), and
        // nothing else is allowed to be.
        SaveService.Instance.NewGame();
        var rooms = new (string MapId, Vector2I From, Rect2I SealedByDesign)[]
        {
            (MapIds.FarmHouse, new(7, 8), new Rect2I(0, 0, 0, 0)),
            (MapIds.GeneralStore, new(7, 8), new Rect2I(1, 1, 12, 3)),
            (MapIds.TownHall, new(20, 21), new Rect2I(0, 0, 0, 0)),
            (MapIds.Barn, new(8, 10), new Rect2I(0, 0, 0, 0)),
            (MapIds.Motel, new(7, 8), new Rect2I(0, 0, 0, 0)),
            (MapIds.GasStation, new(5, 7), new Rect2I(0, 0, 0, 0)),
            // The back bar is sealed by construction, the store's precedent.
            (MapIds.BilliesBar, new(7, 10), new Rect2I(1, 1, 8, 2)),
            (MapIds.Salon, new(6, 7), new Rect2I(0, 0, 0, 0)),
            (MapIds.MotelRoom1, new(4, 5), new Rect2I(0, 0, 0, 0)),
            (MapIds.MotelRoom2, new(4, 5), new Rect2I(0, 0, 0, 0)),
            (MapIds.MotelRoom3, new(4, 5), new Rect2I(0, 0, 0, 0)),
            (MapIds.MotelRoom4, new(4, 5), new Rect2I(0, 0, 0, 0)),
        };

        foreach (var (mapId, from, sealedByDesign) in rooms)
        {
            MapRoot map = MapRegistry.Create(mapId);
            t.Host.AddChild(map);
            await t.WaitFrames(1);
            try
            {
                Rect2I used = map.Ground!.GetUsedRect();
                var open = new HashSet<Vector2I>();
                for (int y = used.Position.Y; y < used.End.Y; y++)
                    for (int x = used.Position.X; x < used.End.X; x++)
                        if (map.IsStandable(new Vector2I(x, y)))
                            open.Add(new Vector2I(x, y));

                t.Assert(open.Contains(from), $"'{mapId}': the flood starts on an open cell");
                var reached = new HashSet<Vector2I> { from };
                var queue = new Queue<Vector2I>();
                queue.Enqueue(from);
                while (queue.Count > 0)
                {
                    Vector2I cell = queue.Dequeue();
                    foreach (Vector2I step in new[]
                             { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
                    {
                        Vector2I next = cell + step;
                        if (open.Contains(next) && reached.Add(next))
                            queue.Enqueue(next);
                    }
                }

                foreach (Vector2I cell in open)
                {
                    if (reached.Contains(cell) || sealedByDesign.HasPoint(cell))
                        continue;
                    t.Assert(false, $"'{mapId}': {cell} is open floor nobody can walk to");
                }
            }
            finally
            {
                map.Free();
                await t.WaitFrames(1);
            }
        }
    }

    private static Door? FindDoor(Node node)
    {
        if (node is Door door)
            return door;
        foreach (Node child in node.GetChildren())
        {
            if (FindDoor(child) is { } found)
                return found;
        }
        return null;
    }

    private static Vector2I TileOf(Vector2 position) => new(
        Mathf.FloorToInt(position.X / MapRoot.TileSize),
        Mathf.FloorToInt(position.Y / MapRoot.TileSize));

    [SimTest]
    public static void Barn_ThreeStatesAreOneSheetAndTwoFlags(TestContext t)
    {
        var data = new GameData();
        t.AssertEqual(BarnRules.Derelict, BarnRules.StateOf(data), "the barn came with the farm");

        data.TrySetFlag(StoryKeys.BarnWeathertight, 4);
        t.AssertEqual(BarnRules.Weathertight, BarnRules.StateOf(data), "patched but unpainted");

        data.TrySetFlag(StoryKeys.BarnRestored, 9);
        t.AssertEqual(BarnRules.Restored, BarnRules.StateOf(data), "barn red and lit");

        // Restored alone is enough — the flags are monotone and a save can only ever have
        // gained them, so the higher one always wins.
        var straightToRestored = new GameData();
        straightToRestored.TrySetFlag(StoryKeys.BarnRestored, 1);
        t.AssertEqual(BarnRules.Restored, BarnRules.StateOf(straightToRestored),
            "the later state does not depend on the earlier flag being present");

        t.AssertEqual(new Rect2(0, 0, 96, 112), BarnFacade.Variant(BarnRules.Derelict), "state 0 rect");
        t.AssertEqual(new Rect2(96, 0, 96, 112), BarnFacade.Variant(BarnRules.Weathertight), "state 1 rect");
        t.AssertEqual(new Rect2(192, 0, 96, 112), BarnFacade.Variant(BarnRules.Restored), "state 2 rect");
        t.AssertEqual(BarnFacade.Variant(BarnRules.Derelict), BarnFacade.Variant(-1),
            "an out-of-range state clamps rather than reading off the sheet");
        t.AssertEqual(BarnFacade.Variant(BarnRules.Restored), BarnFacade.Variant(99), "and at the top end");
    }

    [SimTest]
    public static void Furniture_RectsAreWholeTilesAndStandOnTheirAnchor(TestContext t)
    {
        // Every piece is drawn to STAND ON its anchor cell, so its width must be a whole
        // number of tiles and its height one or two of them. Checked over the whole sheet
        // by reflection rather than a hand-picked list: Furniture.Tiles drives every
        // blocker AddFurniture lays, so a mistyped width silently changes collision in a
        // room nobody was looking at.
        int pieces = 0;
        foreach (FieldInfo field in typeof(Furniture).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(Rect2))
                continue;
            pieces++;
            var source = (Rect2)field.GetValue(null)!;
            string name = field.Name;
            t.AssertEqual(0f, source.Size.X % MapRoot.TileSize, $"{name}: whole tiles wide");
            t.AssertEqual(0f, source.Size.Y % MapRoot.TileSize, $"{name}: whole tiles tall");
            t.AssertEqual(Mathf.RoundToInt(source.Size.X) / MapRoot.TileSize, Furniture.Tiles(source),
                $"{name}: footprint width in tiles");
            t.Assert(source.Size.Y is 16 or 32,
                $"{name}: a piece is one tile tall or one plus an overhang, never more");
            t.Assert(source.Position.X >= 0 && source.Position.Y >= 0
                && source.End.X <= 256 && source.End.Y <= 128,
                $"{name}: {source} lies inside the 256x128 sheet");
        }
        // The handoff's header says 34; its own list enumerates 36 (10 uprights, 12
        // smalls, 8 surfaces, 6 larger) and the sheet draws 36. The list is right.
        t.AssertEqual(36, pieces, "every piece the sheet draws is named");

        // A piece's anchor is the bottom-centre of its base row, exactly like the
        // exterior facades — that is what lets Y-sorting decide front from behind.
        t.AssertEqual(new Vector2(6 * 16 + 16, 5 * 16), Prop.Anchor(6, 4, 2),
            "a two-tile piece anchors on the centre of its base pair's bottom edge");
    }
}
