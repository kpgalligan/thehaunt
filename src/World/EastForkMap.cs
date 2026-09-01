using Godot;
using TheHaunt.Content;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The east fork, 40x30 tiles: the frame between the town centre and the east entry.
/// Abe's shack sits south of the road — he camped here twenty years ago and never
/// left, and never bought property either, which matters more than it looks. North of
/// the road a drive runs toward the mansion and dead-ends at a chain; the ruin itself
/// stays out of frame (src/Content/Places/EastFork.cs). South, east of the shack, a
/// second driveway drops through the trees to the dead drive-in — chained off
/// outside summer trading hours (<see cref="TheHaunt.Content.DriveIn.ChainDown"/>,
/// Kevin's D4): the barrier, its sign, the blocked cells and the gated south exit
/// all derive from that one clock read, polled per frame. The shack ships as a
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

    // The drive-in's driveway: south off the road, through the treeline at the
    // frame's bottom edge ("off the side of the road, west of the east entry" —
    // src/Content/Places/DriveIn.cs).
    private const int TheaterDriveLeft = 33, TheaterDriveRight = 34;

    // The theater chain (Kevin's D4): strung across the drive BETWEEN the
    // from_drive_in arrival row (26) and the south exit (28), so a player still
    // inside at close walks out north of it — never trapped. Woods seal the row
    // either side of the drive's gap.
    private const int TheaterChainRow = 27;

    private TileMapLayer? _obstacles;
    private RoadBarrier? _theaterChain;
    private Sign? _theaterChainSign;
    private bool? _chainDown;

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.EastFork;
        base._EnterTree();
    }

    public override void _Ready()
    {
        BuildSurfaces();
        TileSet tileSet = RoadsideTerrain.Get(); // the paved road needs the roadside source
        TileMapLayer ground = BuildGround(tileSet);
        // Kerb cuts at the mansion drive and the drive-in's driveway.
        ground.AddChild(BuildRoadDressing(RoadTop,
            new[] { (DriveLeft, DriveRight) },
            new[] { (TheaterDriveLeft, TheaterDriveRight) }));

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

        // Deep forest across the north of the frame — the mansion is somewhere beyond
        // it, not on this map — pierced only by the drive, so the chain closes the one
        // gap instead of standing in open grass with strollable ends.
        Fill(1, 1, Width - 2, ChainRow, Surface.Woods);
        Fill(DriveLeft, 1, DriveRight, RoadTop - 1, Surface.Dirt);

        // A worn patch around the shack: twenty years of one man's feet.
        Fill(ShackLeft - 1, ShackTop, ShackRight + 1, ShackBottom + 1, Surface.Dirt);

        // The south treeline the drive pierces (ForkMap's chain pattern): woods
        // seal the chain's row and the one below across the frame, so the chain
        // closes the one gap instead of standing in open grass with strollable
        // ends. Filled BEFORE the drive, whose dirt re-opens the gap.
        Fill(1, TheaterChainRow, Width - 2, TheaterChainRow + 1, Surface.Woods);

        // The drive-in's driveway, south through the woods border.
        Fill(TheaterDriveLeft, RoadBottom + 1, TheaterDriveRight, Height - 1, Surface.Dirt);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        _obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        Block(_obstacles, ChainLeft, ChainRow, ChainRight, ChainRow);
        Block(_obstacles, ShackLeft, ShackTop, ShackRight, ShackBottom);
        // The theater chain's cells are NOT blocked here: RefreshChain paints and
        // erases them with the clock (the farm blockade's toggle pattern).
        AddChild(_obstacles);
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

        _theaterChain = new RoadBarrier
        {
            Name = "TheaterChain",
            TilesWide = TheaterDriveRight - TheaterDriveLeft + 1,
            Position = Prop.Anchor(TheaterDriveLeft, TheaterChainRow,
                TheaterDriveRight - TheaterDriveLeft + 1),
        };
        AddChild(_theaterChain);
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        spawns.AddChild(SpawnMarker("default", 24, 15));
        spawns.AddChild(SpawnMarker("from_town", 2, 15));
        spawns.AddChild(SpawnMarker("from_east_entry", 37, 15));
        // Two rows clear of the south exit (rows 28-29): the arrival frame must not
        // start inside the trigger, or the exit never re-fires and the open border
        // rows below it lead off the world. Also NORTH of the theater chain's row:
        // an arrival from the drive-in must never land trapped behind a raised
        // chain — the way out after close is always open.
        spawns.AddChild(SpawnMarker("from_drive_in", 33, Height - 4));
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

        // The theater chain's board — canon words (the drive-in doc): private
        // property, closed. Present only while the chain is up; its copy is only
        // true then (the farm blockade sign's rule). [KEVIN]
        _theaterChainSign = new Sign
        {
            Name = "TheaterChainSign",
            Position = new Vector2(32 * TileSize + 8, 26 * TileSize + 8),
            Message = "PRIVATE PROPERTY. CLOSED.",
        };
        AddChild(_theaterChainSign);
    }

    private void BuildTravel()
    {
        AddRoadExit("WestExit", MapIds.Town, "from_east_fork", 0, RoadTop);
        AddRoadExit("EastExit", MapIds.EastEntry, "from_east_fork", Width - 1, RoadTop);
        // 2x1, wider than deep, like every north-south mouth: the shape is how
        // GetArrival knows which axis carries an entering player's lane. Gated on
        // the theater chain — belt (the blocked cells) and suspenders (the
        // disabled exit), the farm blockade's pairing. Only the TOWN-LINE mouths
        // are never gated; a private drive may be.
        var southExit = new MapExit
        {
            Name = "SouthExit",
            TargetMapId = MapIds.DriveIn,
            TargetSpawnId = "from_road",
            Position = new Vector2(TheaterDriveLeft * TileSize, (Height - 2) * TileSize)
                + new Vector2(2 * TileSize, TileSize) / 2f,
            IsEnabled = () => DriveIn.ChainDown(Clock.Instance.Now),
        };
        southExit.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(2 * TileSize, TileSize) },
        });
        AddChild(southExit);
    }

    // ------------------------------------------------------------------
    // The theater chain — state is a pure read of the clock (Places/DriveIn), so
    // the map POLLS instead of subscribing: a frozen clock cannot flip it, and a
    // season or hour boundary flips it on the next frame with no repaint call.
    // ------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (_obstacles == null)
            return;
        bool down = DriveIn.ChainDown(Clock.Instance.Now);
        if (_chainDown != down)
            RefreshChain(down);
    }

    private void RefreshChain(bool down)
    {
        _chainDown = down;
        for (int x = TheaterDriveLeft; x <= TheaterDriveRight; x++)
        {
            if (down)
                _obstacles!.EraseCell(new Vector2I(x, TheaterChainRow));
            else
                _obstacles!.SetCell(new Vector2I(x, TheaterChainRow), 0, TerrainTiles.Blocker);
        }
        if (_theaterChain != null)
            _theaterChain.Visible = !down;
        _theaterChainSign?.SetPresent(!down);
    }
}
