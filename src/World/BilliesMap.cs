using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Billie's, 40x30 tiles: the dive bar between the west entry and the fork, and — in
/// the same frame, south of the road — the pit: a covered hole nobody talks about,
/// chained off behind a warning sign (docs/story/README.md). The bar has no art or
/// interior yet, so it ships as a <see cref="PlaceholderBuilding"/>; the pit is a
/// <see cref="PitCover"/> behind a <see cref="RoadBarrier"/>, all of it blocked.
/// </summary>
public partial class BilliesMap : ExteriorMap
{
    private const int Width = 40;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    private const int RoadTop = 14, RoadBottom = 15;

    private const int BarLeft = 14, BarTop = 8, BarRight = 21, BarBottom = 11;

    // The pit's cover, and the chain strung across its approach from the road.
    private const int PitLeft = 26, PitTop = 20, PitRight = 28, PitBottom = 21;
    private const int ChainLeft = 25, ChainRight = 29, ChainRow = 19;

    private static readonly Vector2I Lamp = new(13, 12);

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.Billies;
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

        Fill(BarLeft, BarTop, BarRight, BarBottom + 1, Surface.Gravel);

        // Bare ground around the pit — grass does not grow back over that.
        Fill(PitLeft - 1, ChainRow, PitRight + 1, PitBottom + 1, Surface.Dirt);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        Block(obstacles, BarLeft, BarTop, BarRight, BarBottom);
        Block(obstacles, PitLeft, PitTop, PitRight, PitBottom);
        Block(obstacles, ChainLeft, ChainRow, ChainRight, ChainRow);
        obstacles.SetCell(Lamp, 0, TerrainTiles.Blocker);
        AddChild(obstacles);
    }

    private void BuildStructures()
    {
        AddChild(new PlaceholderBuilding
        {
            Name = "Bar",
            TilesWide = BarRight - BarLeft + 1,
            FootprintRows = BarBottom - BarTop + 1,
            Wall = new Color("6b5a45"),
            Position = Prop.Anchor(BarLeft, BarBottom, BarRight - BarLeft + 1),
        });

        AddChild(new PitCover
        {
            Name = "Pit",
            Position = Prop.Anchor(PitLeft, PitBottom, PitCover.TilesWide),
        });
        AddChild(new RoadBarrier
        {
            Name = "PitChain",
            TilesWide = ChainRight - ChainLeft + 1,
            Position = Prop.Anchor(ChainLeft, ChainRow, ChainRight - ChainLeft + 1),
        });

        AddChild(new LampPost { Position = Prop.Anchor(Lamp.X, Lamp.Y) });
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        spawns.AddChild(SpawnMarker("default", 20, 15));
        spawns.AddChild(SpawnMarker("from_west_entry", 2, 15));
        spawns.AddChild(SpawnMarker("from_fork", 37, 15));
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        // The bar is NAMED in canon — Billie's is Billie's. The pit sign restates the
        // one thing anyone will say about it. [KEVIN] placeholder copy on both.
        AddChild(new Sign
        {
            Name = "BarSign",
            Position = new Vector2(15 * TileSize + 8, 12 * TileSize + 8),
            Message = "Billie's.",
        });
        AddChild(new Sign
        {
            Name = "PitSign",
            Position = new Vector2(24 * TileSize + 8, ChainRow * TileSize + 8),
            Message = "DANGER. KEEP OUT.",
        });
    }

    private void BuildTravel()
    {
        AddRoadExit("WestExit", MapIds.WestEntry, "from_billies", 0, RoadTop);
        AddRoadExit("EastExit", MapIds.Fork, "from_billies", Width - 1, RoadTop);
    }
}
