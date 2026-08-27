using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The east fork, 40x30 tiles: the frame between the town centre and the east entry.
/// Abe's shack sits south of the road — he camped here twenty years ago and never
/// left, and never bought property either, which matters more than it looks. North of
/// the road a drive runs toward the mansion and dead-ends at a chain; the ruin itself
/// stays out of frame (docs/story/README.md). The shack ships as a
/// <see cref="PlaceholderBuilding"/> until it has art.
/// </summary>
public partial class EastForkMap : ExteriorMap
{
    private const int Width = 40;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    private const int RoadTop = 14, RoadBottom = 15;

    // The mansion drive: north out of the road, into the trees, chained well short of
    // wherever it goes. The chain spans exactly the drive's gap in the forest — trees
    // seal the rest of the row, so it cannot be strolled around.
    private const int DriveLeft = 19, DriveRight = 20;
    private const int ChainLeft = 19, ChainRight = 20, ChainRow = 5;

    private const int ShackLeft = 27, ShackTop = 19, ShackRight = 29, ShackBottom = 20;

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.EastFork;
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

        // Deep forest across the north of the frame — the mansion is somewhere beyond
        // it, not on this map — pierced only by the drive, so the chain closes the one
        // gap instead of standing in open grass with strollable ends.
        Fill(1, 1, Width - 2, ChainRow, Surface.Woods);
        Fill(DriveLeft, 1, DriveRight, RoadTop - 1, Surface.Dirt);

        // A worn patch around the shack: twenty years of one man's feet.
        Fill(ShackLeft - 1, ShackTop, ShackRight + 1, ShackBottom + 1, Surface.Dirt);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        Block(obstacles, ChainLeft, ChainRow, ChainRight, ChainRow);
        Block(obstacles, ShackLeft, ShackTop, ShackRight, ShackBottom);
        AddChild(obstacles);
    }

    private void BuildStructures()
    {
        AddChild(new RoadBarrier
        {
            Name = "MansionChain",
            TilesWide = ChainRight - ChainLeft + 1,
            Position = Prop.Anchor(ChainLeft, ChainRow, ChainRight - ChainLeft + 1),
        });

        AddChild(new PlaceholderBuilding
        {
            Name = "Shack",
            TilesWide = ShackRight - ShackLeft + 1,
            FootprintRows = ShackBottom - ShackTop + 1,
            Wall = new Color("6b5f4a"),
            Position = Prop.Anchor(ShackLeft, ShackBottom, ShackRight - ShackLeft + 1),
        });
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        spawns.AddChild(SpawnMarker("default", 24, 15));
        spawns.AddChild(SpawnMarker("from_town", 2, 15));
        spawns.AddChild(SpawnMarker("from_east_entry", 37, 15));
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        // [KEVIN] placeholder copy — the chain admits nothing about the mansion, not
        // even that anyone owns it.
        AddChild(new Sign
        {
            Name = "MansionChainSign",
            Position = new Vector2(18 * TileSize + 8, (ChainRow + 1) * TileSize + 8),
            Message = "KEEP OUT.",
        });
    }

    private void BuildTravel()
    {
        AddRoadExit("WestExit", MapIds.Town, "from_east_fork", 0, RoadTop);
        AddRoadExit("EastExit", MapIds.EastEntry, "from_east_fork", Width - 1, RoadTop);
    }
}
