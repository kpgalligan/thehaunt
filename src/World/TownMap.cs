using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The town centre, 48x30 tiles, painted from the art handoff's terrain sheet
/// (docs/designs/design_handoff_town_art). Grass with an east-west dirt road on rows
/// 14-15 (continuous with every other frame of the strip), a woods edge instead of a
/// wall for the map limit, gravel aprons under the two building facades, a cobbled
/// plaza south of the road, and a road mouth at each end — west to the fork, east to
/// the east fork. Both always enabled: leaving town is never gated. No bed, no
/// farmland (IsTillable stays base false).
///
/// Buildings and props are drawn as base-anchored sprites in elevation (front face
/// only, no side walls). Their collision is a transparent tile on the Obstacles layer,
/// so the geometry the player runs into is unchanged from the procedural placeholder:
/// same footprints, same two door cells.
/// </summary>
public partial class TownMap : ExteriorMap
{
    private const int Width = 48;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    // Town hall: footprint x20-27, y6-11; the facade is 8x8 tiles and overhangs the
    // top two rows. Door cell unchanged.
    private const int HallLeft = 20, HallRight = 27, HallTop = 6, HallBottom = 11;
    private const int DoorX = 23;
    private const int DoorY = 11;

    // General store: footprint x8-14, y8-11; the facade is 7x6 tiles, same overhang.
    private const int StoreLeft = 8, StoreRight = 14, StoreTop = 8, StoreBottom = 11;
    private const int StoreDoorX = 11;
    private const int StoreDoorY = 11;

    // Plaza — the town's social room. The mayor stages on (24,19), so it stays clear.
    private const int PlazaLeft = 22, PlazaRight = 26, PlazaTop = 18, PlazaBottom = 21;
    private static readonly Vector2I PlazaCentre = new(24, 20);

    private const int RoadTop = 14, RoadBottom = 15;
    private const int ApronRow = 12;

    private const string TownHallPath = "res://assets/sprites/town/building_townhall.png";
    private static readonly Rect2 TownHallSource = new(0, 0, 128, 128);

    // Props, as (source rect, base tile, width in tiles). Every one of these blocks.
    private static readonly (Rect2 Source, int X, int Y, int Tiles)[] PlazaProps =
    {
        (TownProps.Well, 25, 19, 2),
        (TownProps.BenchA, 22, 19, 2),
        (TownProps.BenchB, 25, 21, 2),
        (TownProps.NoticeBoard, 21, 17, 2),
        (TownProps.Planters[0], 21, ApronRow, 1),
        (TownProps.Planters[1], 26, ApronRow, 1),
        (TownProps.Planters[2], 9, ApronRow, 1),
    };

    // The 2x2 well is solid for its whole footprint, not just its base row.
    private static readonly Vector2I[] WellExtraBlockers = { new(25, 18), new(26, 18) };

    private static readonly Vector2I[] LampPosts = { new(21, 21), new(27, 21) };

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Town;
        base._EnterTree();
    }

    public override void _Ready()
    {
        BuildSurfaces();
        TileSet tileSet = TownTerrain.Get();
        BuildGround(tileSet);
        BuildObstacles(tileSet);
        BuildFacades();
        BuildProps();
        BuildSpawns();
        BuildInteractables();
        BuildTravel();
    }

    // ------------------------------------------------------------------
    // Surfaces — what each cell IS, before it is any particular tile
    // ------------------------------------------------------------------

    private void BuildSurfaces()
    {
        ResetSurfaces();

        // Road, continuous with the fork's rows 14-15, open at both mouths — west to
        // the fork, east to the east fork.
        for (int x = 0; x < Width; x++)
        {
            Set(x, RoadTop, Surface.Dirt);
            Set(x, RoadBottom, Surface.Dirt);
        }

        // Ground under the facades: hidden by the sprite, gravel so the aprons and
        // door approaches never draw a grass edge against it.
        Fill(HallLeft, HallTop, HallRight, HallBottom, Surface.Gravel);
        Fill(StoreLeft, StoreTop, StoreRight, StoreBottom, Surface.Gravel);
        Fill(HallLeft, ApronRow, HallRight, ApronRow, Surface.Gravel);
        Fill(StoreLeft, ApronRow, StoreRight, ApronRow, Surface.Gravel);

        // Door approaches down to the road. The hall's double door is drawn straddling
        // x23/x24 (only x23 is the collision cell), so its path is two tiles wide to
        // sit under the doorway rather than off to one side of it.
        Fill(DoorX, ApronRow, DoorX + 1, 13, Surface.Dirt);
        Set(StoreDoorX, ApronRow, Surface.Dirt);
        Set(StoreDoorX, 13, Surface.Dirt);

        // Plaza, its apron, and the path down to it from the road. The cobble edge set
        // is drawn over dirt, so the plaza sits in a one-tile apron rather than butting
        // straight into grass.
        Set(PlazaCentre.X, 16, Surface.Dirt);
        Fill(PlazaLeft - 1, PlazaTop - 1, PlazaRight + 1, PlazaBottom + 1, Surface.Dirt);
        Fill(PlazaLeft, PlazaTop, PlazaRight, PlazaBottom, Surface.Cobble);
    }

    // The one Act I dread tell in this map: a paving stone at the plaza centre that is
    // a slightly wrong shape. It is never pointed at and never repeated.
    protected override Vector2I CobbleField(int x, int y) =>
        x == PlazaCentre.X && y == PlazaCentre.Y
            ? TerrainTiles.CobbleWorn
            : base.CobbleField(x, y);

    // ------------------------------------------------------------------
    // Obstacles
    // ------------------------------------------------------------------

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };

        Block(obstacles, HallLeft, HallTop, HallRight, HallBottom, DoorX, DoorY);
        Block(obstacles, StoreLeft, StoreTop, StoreRight, StoreBottom, StoreDoorX, StoreDoorY);

        foreach (var (_, x, y, tiles) in PlazaProps)
            for (int i = 0; i < tiles; i++)
                obstacles.SetCell(new Vector2I(x + i, y), 0, TerrainTiles.Blocker);
        foreach (Vector2I coord in WellExtraBlockers)
            obstacles.SetCell(coord, 0, TerrainTiles.Blocker);
        foreach (Vector2I coord in LampPosts)
            obstacles.SetCell(coord, 0, TerrainTiles.Blocker);

        AddChild(obstacles);
    }

    // ------------------------------------------------------------------
    // Facades and props (drawn in elevation, anchored on their base row)
    // ------------------------------------------------------------------

    private void BuildFacades()
    {
        var hall = new Prop
        {
            Name = "TownHall",
            TexturePath = TownHallPath,
            Source = TownHallSource,
            Position = Prop.Anchor(HallLeft, HallBottom, HallRight - HallLeft + 1),
        };
        // Offsets are the lit pixels' centres, measured from the facade's bottom-centre.
        foreach (float windowX in new[] { -42f, -12f, 18f, 42f })
        {
            hall.AddChild(new GlowLight
            {
                Position = new Vector2(windowX, -61f),
                Size = GlowLight.Falloff.Small,
                Strength = 0.55f,
            });
        }
        hall.AddChild(new GlowLight
        {
            Name = "Fanlight",
            Position = new Vector2(-1f, -42f),
            Size = GlowLight.Falloff.Large,
            Strength = 0.65f,
        });
        hall.AddChild(new GlowLight
        {
            Name = "Cupola",
            Position = new Vector2(-1f, -116f),
            Size = GlowLight.Falloff.Small,
            Strength = 0.5f,
        });
        AddChild(hall);

        AddChild(new StoreFacade
        {
            Name = "GeneralStore",
            TexturePath = StoreFacade.StorePath,
            Source = StoreFacade.OpenVariant,
            Position = Prop.Anchor(StoreLeft, StoreBottom, StoreRight - StoreLeft + 1),
        });
    }

    private void BuildProps()
    {
        foreach (var (source, x, y, tiles) in PlazaProps)
        {
            AddChild(new Prop
            {
                TexturePath = TownProps.TexturePath,
                Source = source,
                Position = Prop.Anchor(x, y, tiles),
            });
        }

        foreach (Vector2I coord in LampPosts)
            AddChild(new LampPost { Position = Prop.Anchor(coord.X, coord.Y) });
    }

    // ------------------------------------------------------------------
    // Spawns / interactables / travel
    // ------------------------------------------------------------------

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        spawns.AddChild(SpawnMarker("from_fork", 2, 15));
        spawns.AddChild(SpawnMarker("from_east_fork", 45, 15));
        spawns.AddChild(new Marker2D
        {
            Name = "from_hall",
            Position = new Vector2(DoorX * TileSize + 8, 13 * TileSize + 8), // (376, 224)
        });
        spawns.AddChild(new Marker2D
        {
            Name = "from_store",
            Position = new Vector2(StoreDoorX * TileSize + 8, 13 * TileSize + 8), // (184, 216)
        });
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        AddChild(new Sign
        {
            Name = "StoreSign",
            Position = new Vector2(12 * TileSize + 8, 12 * TileSize + 8), // (200, 200), beside the door path
            // [KEVIN] placeholder copy — hours restate ShopHours; store NAME not invented.
            Message = "General store. Open 9 to 5.",
        });
    }

    private void BuildTravel()
    {
        // Road mouths — always enabled (IsEnabled null): leaving town is never gated.
        AddRoadExit("WestExit", MapIds.Fork, "from_town", 0, RoadTop);
        AddRoadExit("EastExit", MapIds.EastFork, "from_town", Width - 1, RoadTop);

        // Both doorways are drawn into their facade, so the Door nodes contribute
        // their blocker and their prompt only.
        AddChild(new Door
        {
            Name = "TownHallDoor",
            TargetMapId = MapIds.TownHall,
            TargetSpawnId = "entry",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (376, 184)
        });

        AddChild(new Door
        {
            Name = "StoreDoor",
            TargetMapId = MapIds.GeneralStore,
            TargetSpawnId = "entry",
            DrawPlaceholder = false,
            Position = new Vector2(StoreDoorX * TileSize + 8, StoreDoorY * TileSize + 8), // (184, 184)
        });
    }
}
