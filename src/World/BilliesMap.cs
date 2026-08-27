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

    // The bar's door cell, on the face's bottom row under the drawn doorway (the
    // placeholder draws its door centred, straddling x17/x18).
    private const int BarDoorX = 17;

    // The pit's cover, and the chain strung across its approach from the road.
    private const int PitLeft = 26, PitTop = 20, PitRight = 28, PitBottom = 21;
    private const int ChainLeft = 25, ChainRight = 29, ChainRow = 19;

    private static readonly Vector2I Light = new(13, 13);  // cobra head in the verge

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.Billies;
        base._EnterTree();
    }

    public override void _Ready()
    {
        BuildSurfaces();
        TileSet tileSet = RoadsideTerrain.Get(); // the paved road needs the roadside source
        TileMapLayer ground = BuildGround(tileSet);
        // Kerb cut where the bar's two-tile door path crosses.
        ground.AddChild(BuildRoadDressing(RoadTop,
            new[] { (BarDoorX, BarDoorX + 1) }, System.Array.Empty<(int, int)>()));

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
            Set(x, RoadTop, Surface.Road);
            Set(x, RoadBottom, Surface.Road);
        }

        Fill(BarLeft, BarTop, BarRight, BarBottom + 1, Surface.Gravel);

        // The door path down to the road, two tiles wide under the drawn double door.
        Fill(BarDoorX, BarBottom + 1, BarDoorX + 1, 13, Surface.Dirt);

        // Bare ground around the pit — grass does not grow back over that.
        Fill(PitLeft - 1, ChainRow, PitRight + 1, PitBottom + 1, Surface.Dirt);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        Block(obstacles, BarLeft, BarTop, BarRight, BarBottom, BarDoorX, BarBottom);
        Block(obstacles, PitLeft, PitTop, PitRight, PitBottom);
        Block(obstacles, ChainLeft, ChainRow, ChainRight, ChainRow);
        obstacles.SetCell(Light, 0, TerrainTiles.Blocker);
        AddChild(obstacles);
    }

    private void BuildStructures()
    {
        var bar = new PlaceholderBuilding
        {
            Name = "Bar",
            TilesWide = BarRight - BarLeft + 1,
            FootprintRows = BarBottom - BarTop + 1,
            Wall = new Color("6b5a45"),
            Position = Prop.Anchor(BarLeft, BarBottom, BarRight - BarLeft + 1),
        };
        // The hanging-bracket mount (motel handoff §3): bars get the plaque on an
        // iron arm, one bulb over it, readable side-on down the road. It says BAR and
        // nothing else — a dive doesn't advertise its name, and everyone who matters
        // already knows whose it is.
        bar.AddChild(new BracketSign
        {
            Text = "BAR",
            Position = new Vector2(64, -60),
        });
        AddChild(bar);

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

        AddChild(new StreetLight { Position = Prop.Anchor(Light.X, Light.Y) });
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        spawns.AddChild(SpawnMarker("default", 20, 15));
        spawns.AddChild(SpawnMarker("from_west_entry", 2, 15));
        spawns.AddChild(SpawnMarker("from_fork", 37, 15));
        spawns.AddChild(SpawnMarker("from_bar", BarDoorX, BarBottom + 1));
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

        // The doorway is drawn into the placeholder face, so the Door node
        // contributes its blocker and its prompt only.
        AddChild(new Door
        {
            Name = "BarDoor",
            TargetMapId = MapIds.BilliesBar,
            TargetSpawnId = "entry",
            DrawPlaceholder = false,
            Position = new Vector2(BarDoorX * TileSize + 8, BarBottom * TileSize + 8),
        });
    }
}
