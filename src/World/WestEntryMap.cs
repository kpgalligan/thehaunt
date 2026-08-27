using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The west entry, 48x30 tiles: the frame a stranger sees first. The east-west road on
/// rows 14-15 with the motel long and low on the north side, the gas station across
/// the road, and the fireworks stand further along (docs/story/README.md). The west
/// mouth is the road out of town — and for a resident it only leads back in: walking
/// off the west edge wraps to the east entry's east mouth (<see cref="RoadWrap"/>).
///
/// None of these buildings has art or an interior yet, so each ships as a
/// <see cref="PlaceholderBuilding"/> over Blocker cells with a sign naming it — the
/// same ship-before-art route the pre-art town used.
/// </summary>
public partial class WestEntryMap : ExteriorMap
{
    private const int Width = 48;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    private const int RoadTop = 14, RoadBottom = 15;

    // Footprints: (left, top, right, bottom). Faces are drawn two rows taller.
    private const int MotelLeft = 8, MotelTop = 8, MotelRight = 17, MotelBottom = 11;
    private const int GasLeft = 24, GasTop = 18, GasRight = 29, GasBottom = 20;
    private const int StandLeft = 33, StandTop = 10, StandRight = 35, StandBottom = 11;

    private static readonly Vector2I Lamp = new(18, 12);

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.WestEntry;
        base._EnterTree();
    }

    public override void _Ready()
    {
        BuildSurfaces();
        TileSet tileSet = TownTerrain.Get();
        BuildGround(tileSet);
        BuildObstacles(tileSet);
        BuildBuildings();
        BuildSpawns();
        BuildInteractables();
        BuildTravel();
    }

    private void BuildSurfaces()
    {
        ResetSurfaces();

        // The road, open at both mouths: west out of town, east toward Billie's.
        for (int x = 0; x < Width; x++)
        {
            Set(x, RoadTop, Surface.Dirt);
            Set(x, RoadBottom, Surface.Dirt);
        }

        // Gravel under each building and one apron row below, so no face draws a
        // grass edge against its own frontage.
        Fill(MotelLeft, MotelTop, MotelRight, MotelBottom + 1, Surface.Gravel);
        Fill(GasLeft, GasTop, GasRight, GasBottom + 1, Surface.Gravel);
        Fill(StandLeft, StandTop, StandRight, StandBottom + 1, Surface.Gravel);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        Block(obstacles, MotelLeft, MotelTop, MotelRight, MotelBottom);
        Block(obstacles, GasLeft, GasTop, GasRight, GasBottom);
        Block(obstacles, StandLeft, StandTop, StandRight, StandBottom);
        obstacles.SetCell(Lamp, 0, TerrainTiles.Blocker);
        AddChild(obstacles);
    }

    private void BuildBuildings()
    {
        AddChild(new PlaceholderBuilding
        {
            Name = "Motel",
            TilesWide = MotelRight - MotelLeft + 1,
            FootprintRows = MotelBottom - MotelTop + 1,
            Wall = new Color("8a8578"),
            Position = Prop.Anchor(MotelLeft, MotelBottom, MotelRight - MotelLeft + 1),
        });
        AddChild(new PlaceholderBuilding
        {
            Name = "GasStation",
            TilesWide = GasRight - GasLeft + 1,
            FootprintRows = GasBottom - GasTop + 1,
            Wall = new Color("8a7a6a"),
            Position = Prop.Anchor(GasLeft, GasBottom, GasRight - GasLeft + 1),
        });
        AddChild(new PlaceholderBuilding
        {
            Name = "FireworksStand",
            TilesWide = StandRight - StandLeft + 1,
            FootprintRows = StandBottom - StandTop + 1,
            Wall = new Color("8a6a45"),
            Position = Prop.Anchor(StandLeft, StandBottom, StandRight - StandLeft + 1),
        });

        AddChild(new LampPost { Position = Prop.Anchor(Lamp.X, Lamp.Y) });
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule). The
        // wrap marker is where a resident who left east finds themselves arriving.
        spawns.AddChild(SpawnMarker("default", 24, 15));
        spawns.AddChild(SpawnMarker(RoadWrap.ArrivalSpawn, 2, 15));
        spawns.AddChild(SpawnMarker("from_billies", 45, 15));
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        // [KEVIN] placeholder copy on all three — canon restatement only, no names.
        AddChild(new Sign
        {
            Name = "MotelSign",
            Position = new Vector2(9 * TileSize + 8, 12 * TileSize + 8),
            Message = "Motel. Vacancy.",
        });
        AddChild(new Sign
        {
            Name = "GasSign",
            // South of the footprint: a sign north of a south-of-road building lands
            // inside its drawn face and Y-sorts invisible.
            Position = new Vector2(26 * TileSize + 8, 21 * TileSize + 8),
            Message = "Gas.",
        });
        AddChild(new Sign
        {
            Name = "FireworksSign",
            Position = new Vector2(36 * TileSize + 8, 12 * TileSize + 8),
            Message = "Fireworks.",
        });
    }

    private void BuildTravel()
    {
        // The road out. It goes exactly where the story says it goes.
        AddRoadExit("WestExit", RoadWrap.PastTheWestEdgeMap, RoadWrap.ArrivalSpawn, 0, RoadTop);
        AddRoadExit("EastExit", MapIds.Billies, "from_west_entry", Width - 1, RoadTop);
    }
}
