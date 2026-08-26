using Godot;

namespace TheHaunt.World;

/// <summary>
/// The shell every interior shares, so a new room costs a layout function rather than a
/// map class: the oversized near-black <c>Surround</c> behind Ground, a floor fill, a
/// single-thickness wall ring with the Door flush in the south wall, and the helpers for
/// dropping fixtures and furniture on top.
///
/// Structure is unchanged from the procedural placeholder these rooms shipped as — only
/// the tile source moved to the drawn sheet (farm/interiors handoff §5). Walls and
/// fixtures are painted on the Obstacles layer, which is both the visual and the
/// collision; sprite-drawn furniture takes a transparent blocker on the same layer,
/// exactly as the town's facades do.
///
/// Layer order: Surround, Ground, Obstacles, Dressing. Dressing exists for the one tile
/// in the sheet with an alpha channel — the cobweb, which is composited OVER whatever it
/// hangs on rather than replacing it.
/// </summary>
public abstract partial class InteriorMap : MapRoot
{
    /// <summary>Indoors: fixed warm key, never the day/night tint.</summary>
    public sealed override bool IsInterior => true;

    protected abstract int Width { get; }
    protected abstract int Height { get; }

    /// <summary>Wall material for this building (handoff §5 matches one set per building).</summary>
    protected abstract InteriorTiles.WallSet Walls { get; }

    /// <summary>
    /// Floor variants, indexed by <c>(x + y) % Length</c>. The reference rooms all use
    /// that diagonal stagger rather than a hash — two variants alternate, three step.
    /// </summary>
    protected abstract Vector2I[] Floor { get; }

    protected abstract int DoorX { get; }
    protected abstract int DoorY { get; }

    private TileMapLayer _ground = null!;
    private TileMapLayer _obstacles = null!;
    private TileMapLayer _dressing = null!;

    public override void _Ready()
    {
        BuildSurround();
        TileSet tileSet = InteriorTerrain.Get();

        _ground = new TileMapLayer { Name = "Ground", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                SetFloorOn(_ground, x, y, Floor[(x + y) % Floor.Length]);
        AddChild(_ground);

        _obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        BuildWallRing();
        AddChild(_obstacles);

        _dressing = new TileMapLayer { Name = "Dressing", TileSet = tileSet };
        AddChild(_dressing);

        // Threshold on the floor cell just inside the door — the one place the eye is
        // told where the room begins (handoff §5).
        SetFloor(DoorX, Height - 2, InteriorTiles.Threshold);

        Decorate();
        BuildSpawns();
        BuildInteractables();
    }

    /// <summary>Fixtures, furniture and dressing. Runs with every layer in the tree.</summary>
    protected abstract void Decorate();

    protected abstract void BuildSpawns();

    protected abstract void BuildInteractables();

    // ------------------------------------------------------------------
    // Shell
    // ------------------------------------------------------------------

    /// <summary>
    /// Oversized near-black backdrop, added before Ground so it draws behind everything.
    /// <see cref="MapRoot.GetCameraLimits"/> grows the limits to at least the viewport
    /// around a room far smaller than it; the overshoot must read as darkness, not the
    /// clear color. MouseFilter Ignore so the giant Control never swallows tool clicks.
    /// </summary>
    private void BuildSurround()
    {
        AddChild(new ColorRect
        {
            Name = "Surround",
            Color = new Color("0e0e12"),
            Position = new Vector2(-ViewportWidth, -ViewportHeight),
            Size = new Vector2(
                ViewportWidth * 2 + Width * TileSize, ViewportHeight * 2 + Height * TileSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    private void BuildWallRing()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool side = x == 0 || x == Width - 1;
                bool north = y == 0;
                bool south = y == Height - 1;
                if (!side && !north && !south)
                    continue;

                // door_open is the sheet's one walkable wall tile, so it draws the
                // opening without blocking it; the Door node carries the blocker on that
                // cell, exactly as it did when the ring left the gap empty.
                if (south && x == DoorX)
                {
                    SetWall(x, y, InteriorTiles.DoorOpen);
                    continue;
                }

                // The north row takes the cornice course: its dark top edge is what
                // reads as ceiling shadow above the wall. The reference rooms run it
                // straight into the corners rather than using wall_cnr_l/r, which are
                // drawn as plaster pilasters and only match a plaster wall.
                SetWall(x, y, north ? Walls.Cornice : Walls.Lower);
            }
        }
    }

    // ------------------------------------------------------------------
    // Painting helpers
    // ------------------------------------------------------------------

    protected void SetFloor(int x, int y, Vector2I tile) => SetFloorOn(_ground, x, y, tile);

    private static void SetFloorOn(TileMapLayer layer, int x, int y, Vector2I tile) =>
        layer.SetCell(new Vector2I(x, y), InteriorTerrain.TileSource,
            InteriorTiles.ForAct(tile, InteriorTiles.Act.One));

    /// <summary>Paints a solid sheet tile on Obstacles — the visual AND the collision.</summary>
    protected void SetWall(int x, int y, Vector2I tile) =>
        _obstacles.SetCell(new Vector2I(x, y), InteriorTerrain.TileSource,
            InteriorTiles.ForAct(tile, InteriorTiles.Act.One));

    /// <summary>A run of tiles left to right — hearths and counters assemble this way.</summary>
    protected void SetWallRun(int x0, int y, params Vector2I[] tiles)
    {
        for (int i = 0; i < tiles.Length; i++)
            SetWall(x0 + i, y, tiles[i]);
    }

    /// <summary>
    /// A three-piece hearth on (<paramref name="x"/>..x+2, <paramref name="y"/>) with its
    /// fire in the cell below the centre — the fire tile has no lintel of its own, so it
    /// only ever goes underneath (handoff §5, confirmed by both reference rooms).
    /// </summary>
    protected void AddHearth(int x, int y)
    {
        SetWallRun(x, y, InteriorTiles.HearthL, InteriorTiles.HearthC, InteriorTiles.HearthR);
        SetWall(x + 1, y + 1, InteriorTiles.HearthFire);
    }

    /// <summary>
    /// A counter run from <paramref name="x0"/> to <paramref name="x1"/>: panelled ends,
    /// plain middle.
    /// </summary>
    protected void AddCounter(int x0, int x1, int y)
    {
        for (int x = x0; x <= x1; x++)
        {
            SetWall(x, y,
                x == x0 ? InteriorTiles.CounterL
                : x == x1 ? InteriorTiles.CounterR
                : InteriorTiles.CounterC);
        }
    }

    /// <summary>Blocks a cell without drawing anything — collision for a sprite.</summary>
    protected void Block(int x, int y) =>
        _obstacles.SetCell(new Vector2I(x, y), InteriorTerrain.BlockerSource, InteriorTerrain.Blocker);

    /// <summary>
    /// Hangs the sheet's one alpha tile over whatever the cell already holds. It has to
    /// go on its own layer: a cobweb replacing a hayloft edge would delete the loft.
    /// </summary>
    protected void AddCobweb(int x, int y) =>
        _dressing.SetCell(new Vector2I(x, y), InteriorTerrain.TileSource,
            InteriorTiles.ForAct(InteriorTiles.Cobweb, InteriorTiles.Act.One));

    protected void ClearDressing(int x, int y) => _dressing.EraseCell(new Vector2I(x, y));

    /// <summary>
    /// Drops a furniture piece standing on (<paramref name="x"/>, <paramref name="y"/>)
    /// and blocks the cells its base covers. Pass <paramref name="blocks"/> false for
    /// anything the player should walk over or an NPC should stand on.
    /// </summary>
    protected Prop AddFurniture(Rect2 source, int x, int y, bool blocks = true)
    {
        int tiles = Furniture.Tiles(source);
        var prop = new Prop
        {
            TexturePath = Furniture.TexturePath,
            Source = source,
            Position = Prop.Anchor(x, y, tiles),
        };
        AddChild(prop);
        if (blocks)
        {
            for (int i = 0; i < tiles; i++)
                Block(x + i, y);
        }
        return prop;
    }
}
