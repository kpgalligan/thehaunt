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
    // The chain's board: its blocker returns with the chain.
    private const int TheaterChainSignX = 32, TheaterChainSignY = 26;
    private const string TheaterChainSignId = "TheaterChainSign";

    private TileMapLayer? _obstacles;
    private RoadBarrier? _theaterChain;
    private Vector2I? _theaterChainCell;   // the chain's west cell; it spans two
    private Sign? _theaterChainSign;
    private bool? _chainDown;

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.EastFork;
        base._EnterTree();
    }

    protected override void BuildDefaultSurfaces()
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

    /// <summary>The east fork's placements as the C# literals describe them: the seed
    /// <c>data/maps/east_fork.tmx</c> was written from, and the fallback when it is
    /// missing. The kerb cuts at the mansion drive and the drive-in's driveway derive
    /// from their dirt.</summary>
    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.EastFork);

        recipe.Add(PlacementKinds.Prop, "mansion_chain", ChainLeft, ChainRow);
        recipe.Add(PlacementKinds.Prop, "shack", ShackLeft, ShackBottom);
        recipe.Add(PlacementKinds.Prop, "theater_chain", TheaterDriveLeft, TheaterChainRow);

        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        recipe.Add(PlacementKinds.Spawn, "default", 24, 15);
        recipe.Add(PlacementKinds.Spawn, "from_town", 2, 15);
        recipe.Add(PlacementKinds.Spawn, "from_east_entry", 37, 15);
        // Two rows clear of the south exit (rows 28-29): the arrival frame must not
        // start inside the trigger, or the exit never re-fires and the open border
        // rows below it lead off the world. Also NORTH of the theater chain's row:
        // an arrival from the drive-in must never land trapped behind a raised
        // chain — the way out after close is always open.
        recipe.Add(PlacementKinds.Spawn, "from_drive_in", 33, Height - 4);

        // [KEVIN] placeholder copy — the chain admits nothing about the mansion, not
        // even that anyone owns it. The theater chain's board is present only while
        // the chain is up; its copy is only true then (the farm blockade sign's rule).
        // Words with the place (src/Content/Places/EastFork.cs).
        recipe.Add(PlacementKinds.Sign, "MansionChainSign", 18, ChainRow + 1);
        recipe.Add(PlacementKinds.Sign, TheaterChainSignId, TheaterChainSignX, TheaterChainSignY);

        // 2x1, wider than deep, like every north-south mouth: the shape is how
        // GetArrival knows which axis carries an entering player's lane. The drive-in
        // mouth is gated on the theater chain (ExitGate).
        foreach ((string target, string spawn, int x, int y, int w, int h) in new[]
        {
            (MapIds.Town, "from_east_fork", 0, RoadTop, 1, 2),
            (MapIds.EastEntry, "from_east_fork", Width - 1, RoadTop, 1, 2),
            (MapIds.DriveIn, "from_road", TheaterDriveLeft, Height - 2, 2, 1),
        })
        {
            MapPlacement exit = recipe.Add(PlacementKinds.Exit, target, x, y);
            exit.SetText(PlacementFields.Spawn, spawn);
            exit.SetInt(PlacementFields.Width, w);
            exit.SetInt(PlacementFields.Height, h);
        }

        return recipe;
    }

    protected override string? SignTextFor(string signId) => EastFork.SignTextFor(signId);

    protected override IReadOnlyDictionary<string, ExteriorProp> PropCatalog() =>
        new Dictionary<string, ExteriorProp>(StringComparer.Ordinal)
        {
            ["mansion_chain"] = new(p => new RoadBarrier
            {
                Name = "MansionChain",
                TilesWide = ChainRight - ChainLeft + 1,
                Position = Prop.Anchor(p.X, p.Y, ChainRight - ChainLeft + 1),
            }, ExteriorProp.Rows(ChainRight - ChainLeft + 1, 1)),
            ["shack"] = new(p => new PlaceholderBuilding
            {
                Name = "Shack",
                TilesWide = ShackRight - ShackLeft + 1,
                FootprintRows = ShackBottom - ShackTop + 1,
                Wall = new Color("6b5f4a"),
                Position = Prop.Anchor(p.X, p.Y, ShackRight - ShackLeft + 1),
            }, ExteriorProp.Rows(ShackRight - ShackLeft + 1, ShackBottom - ShackTop + 1)),
            // The theater chain's cells are NOT blocked here: RefreshChain paints and
            // erases them with the clock (the farm blockade's toggle pattern).
            ["theater_chain"] = new(BuildTheaterChain),
        };

    private Node2D BuildTheaterChain(MapPlacement p)
    {
        _theaterChainCell = p.Cell;
        _theaterChain = new RoadBarrier
        {
            Name = "TheaterChain",
            TilesWide = TheaterDriveRight - TheaterDriveLeft + 1,
            Position = Prop.Anchor(p.X, p.Y, TheaterDriveRight - TheaterDriveLeft + 1),
        };
        return _theaterChain;
    }

    // The gate reads the chain's ACTUAL state rather than the clock: while a raise is
    // held back (see _Process) the drive is still open, so the exit must still work —
    // belt (the blocked cells) and suspenders (the disabled exit), the farm blockade's
    // pairing. Only the TOWN-LINE mouths are never gated; a private drive may be.
    protected override Func<bool>? ExitGate(string exitId) =>
        exitId == MapIds.DriveIn ? () => _chainDown == true : null;

    protected override void OnBuilt()
    {
        _obstacles = GetNode<TileMapLayer>("Obstacles");
        _theaterChainSign = GetNodeOrNull<Sign>(TheaterChainSignId);
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
        if (_chainDown == down)
            return;
        // Raising is the one direction that can put a collider on the player, and
        // nothing else in the game raises collision under someone's feet at runtime
        // (the farm blockade only clears; the garage bays stay blocked for exactly
        // this reason). Hold the chain down while the feet box overlaps its cells
        // or the sign's, and retry next frame; the exit stays open meanwhile.
        if (!down && PlayerOnChainCells())
            return;
        RefreshChain(down);
    }

    private bool PlayerOnChainCells()
    {
        if (GetTree().GetFirstNodeInGroup(PlayerGroup) is not Node2D player)
            return false;
        var feet = new Rect2(player.GlobalPosition + PlayerFeetBox.Position, PlayerFeetBox.Size);
        if (_theaterChainCell is { } chain)
        {
            var chainCells = new Rect2(chain.X * TileSize, chain.Y * TileSize, 2 * TileSize, TileSize);
            if (feet.Intersects(chainCells.Grow(1)))
                return true;
        }
        if (_theaterChainSign != null)
        {
            Vector2 cell = (_theaterChainSign.Position / TileSize).Floor() * TileSize;
            var signCell = new Rect2(cell, TileSize, TileSize);
            if (feet.Intersects(signCell.Grow(1)))
                return true;
        }
        return false;
    }

    private void RefreshChain(bool down)
    {
        _chainDown = down;
        if (_theaterChainCell is { } chain)
        {
            for (int x = chain.X; x <= chain.X + 1; x++)
            {
                if (down)
                    _obstacles!.EraseCell(new Vector2I(x, chain.Y));
                else
                    _obstacles!.SetCell(new Vector2I(x, chain.Y), 0, TerrainTiles.Blocker);
            }
        }
        if (_theaterChain != null)
            _theaterChain.Visible = !down;
        _theaterChainSign?.SetPresent(!down);
    }
}
