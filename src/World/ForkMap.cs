using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The fork, 40x30 tiles: the crossroads west of the town centre. East-west road on
/// rows 14-15 (Billie's to the west, town to the east), the farm road running north
/// out of the frame, and a southbound stub that is chained off — it leads to something
/// later, and the chain says so without saying what (docs/story/README.md). No
/// buildings; this frame is all road.
/// </summary>
public partial class ForkMap : ExteriorMap
{
    private const int Width = 40;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    private const int RoadTop = 14, RoadBottom = 15;

    // The north-south road's columns. North runs out of the frame to the farm; south
    // dead-ends into the trees behind the chain.
    private const int CrossLeft = 19, CrossRight = 20;
    private const int SouthStubEnd = 28;

    // The chain spans exactly the road gap in the southern treeline — trees seal the
    // rest of the row, so it cannot be strolled around.
    private const int ChainLeft = 19, ChainRight = 20, ChainRow = 24;

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.Fork;
        base._EnterTree();
    }

    public override void _Ready()
    {
        BuildSurfaces();
        TileSet tileSet = TownTerrain.Get();
        BuildGround(tileSet);
        BuildObstacles(tileSet);
        BuildStructures();
        BuildSpawns();
        BuildInteractables();
        BuildTravel();
    }

    private void BuildSurfaces()
    {
        ResetSurfaces();

        for (int x = 0; x < Width; x++)
        {
            Set(x, RoadTop, Surface.Dirt);
            Set(x, RoadBottom, Surface.Dirt);
        }

        // The farm road, open through the north border.
        Fill(CrossLeft, 0, CrossRight, RoadTop - 1, Surface.Dirt);

        // Forest across the whole south of the frame, pierced only by the road stub —
        // so the chain closes the one gap instead of standing in open grass with
        // strollable ends. The stub dead-ends against the border woods.
        Fill(1, ChainRow, Width - 2, Height - 2, Surface.Woods);
        Fill(CrossLeft, RoadBottom + 1, CrossRight, SouthStubEnd, Surface.Dirt);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        Block(obstacles, ChainLeft, ChainRow, ChainRight, ChainRow);
        AddChild(obstacles);
    }

    private void BuildStructures()
    {
        AddChild(new RoadBarrier
        {
            Name = "SouthChain",
            TilesWide = ChainRight - ChainLeft + 1,
            Position = Prop.Anchor(ChainLeft, ChainRow, ChainRight - ChainLeft + 1),
        });
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        spawns.AddChild(SpawnMarker("default", 24, 15));
        spawns.AddChild(SpawnMarker("from_billies", 2, 15));
        spawns.AddChild(SpawnMarker("from_town", 37, 15));
        spawns.AddChild(SpawnMarker("from_farm", 19, 2));
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        // [KEVIN] placeholder copy on both — directions restate the map graph, and the
        // chain's sign says nothing about what is behind it, which is the point.
        AddChild(new Sign
        {
            Name = "FingerPost",
            Position = new Vector2(22 * TileSize + 8, 13 * TileSize + 8),
            Message = "North: the farm. East: town. West: the west road.",
        });
        AddChild(new Sign
        {
            Name = "SouthChainSign",
            Position = new Vector2(18 * TileSize + 8, (ChainRow - 1) * TileSize + 8),
            Message = "Road closed.",
        });
    }

    private void BuildTravel()
    {
        AddRoadExit("WestExit", MapIds.Billies, "from_fork", 0, RoadTop);
        AddRoadExit("EastExit", MapIds.Town, "from_fork", Width - 1, RoadTop);
        AddRoadExit("NorthExit", MapIds.Farm, "road", CrossLeft, 0, widthTiles: 2, heightTiles: 1);
    }
}
