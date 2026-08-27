using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The east entry, 48x30 tiles: the police station and the hardware store north of
/// the road, the hair salon across it (docs/story/README.md). The hardware store
/// starts closed — its owner is in the hospital, though the sign says only that it
/// is closed. The
/// east mouth is the other road out of town, and for a resident it wraps to the west
/// entry's west mouth (<see cref="RoadWrap"/>). All three buildings ship as
/// <see cref="PlaceholderBuilding"/>s until they have art.
/// </summary>
public partial class EastEntryMap : ExteriorMap
{
    private const int Width = 48;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    private const int RoadTop = 14, RoadBottom = 15;

    private const int PoliceLeft = 9, PoliceTop = 8, PoliceRight = 16, PoliceBottom = 11;
    private const int HardwareLeft = 22, HardwareTop = 9, HardwareRight = 28, HardwareBottom = 11;
    private const int SalonLeft = 32, SalonTop = 18, SalonRight = 36, SalonBottom = 20;

    // The salon's door cell, on the face's bottom row under the drawn doorway.
    private const int SalonDoorX = 34;

    private static readonly Vector2I Lamp = new(17, 12);

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.EastEntry;
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

        // The road, open at both mouths: west toward the east fork, east out of town.
        for (int x = 0; x < Width; x++)
        {
            Set(x, RoadTop, Surface.Dirt);
            Set(x, RoadBottom, Surface.Dirt);
        }

        Fill(PoliceLeft, PoliceTop, PoliceRight, PoliceBottom + 1, Surface.Gravel);
        Fill(HardwareLeft, HardwareTop, HardwareRight, HardwareBottom + 1, Surface.Gravel);
        Fill(SalonLeft, SalonTop, SalonRight, SalonBottom + 1, Surface.Gravel);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        Block(obstacles, PoliceLeft, PoliceTop, PoliceRight, PoliceBottom);
        Block(obstacles, HardwareLeft, HardwareTop, HardwareRight, HardwareBottom);
        Block(obstacles, SalonLeft, SalonTop, SalonRight, SalonBottom, SalonDoorX, SalonBottom);
        obstacles.SetCell(Lamp, 0, TerrainTiles.Blocker);
        AddChild(obstacles);
    }

    private void BuildBuildings()
    {
        AddChild(new PlaceholderBuilding
        {
            Name = "PoliceStation",
            TilesWide = PoliceRight - PoliceLeft + 1,
            FootprintRows = PoliceBottom - PoliceTop + 1,
            Wall = new Color("7a8290"),
            Position = Prop.Anchor(PoliceLeft, PoliceBottom, PoliceRight - PoliceLeft + 1),
        });
        AddChild(new PlaceholderBuilding
        {
            Name = "HardwareStore",
            TilesWide = HardwareRight - HardwareLeft + 1,
            FootprintRows = HardwareBottom - HardwareTop + 1,
            Wall = new Color("8a7a5a"),
            Position = Prop.Anchor(HardwareLeft, HardwareBottom, HardwareRight - HardwareLeft + 1),
        });
        AddChild(new PlaceholderBuilding
        {
            Name = "Salon",
            TilesWide = SalonRight - SalonLeft + 1,
            FootprintRows = SalonBottom - SalonTop + 1,
            Wall = new Color("9a8a8a"),
            Position = Prop.Anchor(SalonLeft, SalonBottom, SalonRight - SalonLeft + 1),
        });

        AddChild(new LampPost { Position = Prop.Anchor(Lamp.X, Lamp.Y) });
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule). The
        // wrap marker is where a resident who left west finds themselves arriving.
        spawns.AddChild(SpawnMarker("default", 24, 15));
        spawns.AddChild(SpawnMarker("from_east_fork", 2, 15));
        spawns.AddChild(SpawnMarker(RoadWrap.ArrivalSpawn, 45, 15));
        spawns.AddChild(SpawnMarker("from_salon", SalonDoorX, SalonBottom + 1));
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        // [KEVIN] placeholder copy on all three — canon restatement only, no names.
        AddChild(new Sign
        {
            Name = "PoliceSign",
            Position = new Vector2(10 * TileSize + 8, 12 * TileSize + 8),
            Message = "Police.",
        });
        AddChild(new Sign
        {
            Name = "HardwareSign",
            Position = new Vector2(23 * TileSize + 8, 12 * TileSize + 8),
            Message = "Hardware. Closed until further notice.",
        });
        AddChild(new Sign
        {
            Name = "SalonSign",
            // South of the footprint: a sign north of a south-of-road building lands
            // inside its drawn face and Y-sorts invisible. West of the doorway so the
            // door approach stays clear.
            Position = new Vector2(32 * TileSize + 8, 21 * TileSize + 8),
            Message = "Salon.",
        });
    }

    private void BuildTravel()
    {
        AddRoadExit("WestExit", MapIds.EastFork, "from_east_entry", 0, RoadTop);
        // The road out. It goes exactly where the story says it goes.
        AddRoadExit("EastExit", RoadWrap.PastTheEastEdgeMap, RoadWrap.ArrivalSpawn, Width - 1, RoadTop);

        // The doorway is drawn into the placeholder face, so the Door node
        // contributes its blocker and its prompt only.
        AddChild(new Door
        {
            Name = "SalonDoor",
            TargetMapId = MapIds.Salon,
            TargetSpawnId = "entry",
            DrawPlaceholder = false,
            Position = new Vector2(SalonDoorX * TileSize + 8, SalonBottom * TileSize + 8),
        });
    }
}
