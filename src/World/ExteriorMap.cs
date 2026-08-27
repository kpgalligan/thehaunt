using Godot;

namespace TheHaunt.World;

/// <summary>
/// Base for every exterior frame painted from the town terrain sheet: a woods border
/// for the map limit (forest that turns you around, never a wall), grass under
/// everything, and the shared painters that turn a <see cref="Surface"/> grid into
/// act-wrapped tiles. The edge painters live here because all of these frames read
/// off the same sheet with the same neighbour rules — a map that painted its own dirt
/// edges would disagree with the road one frame over.
///
/// A subclass owns its geometry: fill the grid (starting from
/// <see cref="ResetSurfaces"/>), then hand it to <see cref="BuildGround"/>. Everything
/// painted goes through <see cref="TerrainTiles.ForAct"/> — the act swap is a flag
/// check, never a re-lay.
/// </summary>
public abstract partial class ExteriorMap : MapRoot
{
    protected enum Surface { Grass, Dirt, Gravel, Cobble, Woods }

    protected abstract int MapWidth { get; }
    protected abstract int MapHeight { get; }

    protected virtual TerrainTiles.Act CurrentAct => TerrainTiles.Act.One;

    private Surface[,] _surface = new Surface[0, 0];

    // ------------------------------------------------------------------
    // Surfaces — what each cell IS, before it is any particular tile
    // ------------------------------------------------------------------

    /// <summary>Woods ring, grass interior — the diegetic map limit every frame starts from.</summary>
    protected void ResetSurfaces()
    {
        _surface = new Surface[MapWidth, MapHeight];
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                bool border = x == 0 || x == MapWidth - 1 || y == 0 || y == MapHeight - 1;
                _surface[x, y] = border ? Surface.Woods : Surface.Grass;
            }
        }
    }

    protected void Set(int x, int y, Surface surface) => _surface[x, y] = surface;

    protected void Fill(int x0, int y0, int x1, int y1, Surface surface)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                _surface[x, y] = surface;
    }

    protected Surface At(int x, int y) =>
        x < 0 || y < 0 || x >= MapWidth || y >= MapHeight ? Surface.Woods : _surface[x, y];

    // Dirt, gravel and cobble are all "made ground": the dirt-over-grass set only
    // draws an edge where a cell actually meets grass or woods.
    private bool IsGrassy(int x, int y) => At(x, y) is Surface.Grass or Surface.Woods;

    // ------------------------------------------------------------------
    // Ground
    // ------------------------------------------------------------------

    protected TileMapLayer BuildGround(TileSet tileSet)
    {
        var ground = new TileMapLayer { Name = "Ground", TileSet = tileSet };
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                Vector2I tile = _surface[x, y] switch
                {
                    Surface.Dirt => PaintDirt(x, y),
                    Surface.Gravel => Pick(TerrainTiles.Gravel, x, y),
                    Surface.Cobble => PaintCobble(x, y),
                    Surface.Woods => PaintWoods(x, y),
                    _ => PaintGrass(x, y),
                };
                ground.SetCell(new Vector2I(x, y), 0, TerrainTiles.ForAct(tile, CurrentAct));
            }
        }
        AddChild(ground);
        return ground;
    }

    private Vector2I PaintGrass(int x, int y)
    {
        // Detail tiles never adjacent to each other: the checkerboard parity rules out
        // any orthogonal neighbour before the frequency test runs, so the draw below is
        // over half the cells and the rates double — 3% clover, 2% stones, and the bare
        // patch rarer still. Much past that and it reads as noise.
        if ((x + y) % 2 == 0)
        {
            int roll = Hash(x, y) % 100;
            if (roll < 6) return TerrainTiles.GrassClover;
            if (roll < 10) return TerrainTiles.GrassStones;
            if (roll < 11) return TerrainTiles.GrassBare;
        }
        return Pick(TerrainTiles.Grass, x, y);
    }

    private Vector2I PaintDirt(int x, int y)
    {
        bool grassN = IsGrassy(x, y - 1), grassE = IsGrassy(x + 1, y);
        bool grassS = IsGrassy(x, y + 1), grassW = IsGrassy(x - 1, y);
        if (grassN || grassE || grassS || grassW)
            return TerrainTiles.DirtEdge(grassN, grassE, grassS, grassW);

        Vector2I? inner = TerrainTiles.DirtInnerCorner(
            IsGrassy(x + 1, y - 1), IsGrassy(x + 1, y + 1),
            IsGrassy(x - 1, y + 1), IsGrassy(x - 1, y - 1));
        return inner ?? Pick(TerrainTiles.Dirt, x, y);
    }

    private Vector2I PaintCobble(int x, int y)
    {
        Vector2I? kerb = TerrainTiles.Kerb(
            At(x, y - 1) != Surface.Cobble, At(x + 1, y) != Surface.Cobble,
            At(x, y + 1) != Surface.Cobble, At(x - 1, y) != Surface.Cobble);
        return kerb ?? CobbleField(x, y);
    }

    /// <summary>The interior cobble a non-kerb plaza cell takes — the town overrides this
    /// to place its one worn stone.</summary>
    protected virtual Vector2I CobbleField(int x, int y) => Pick(TerrainTiles.Cobble, x, y);

    private Vector2I PaintWoods(int x, int y)
    {
        if (x == 0 && y == 0) return TerrainTiles.WoodsCornerSe;
        if (x == MapWidth - 1 && y == 0) return TerrainTiles.WoodsCornerSw;
        if (x == MapWidth - 1 && y == MapHeight - 1) return TerrainTiles.WoodsCornerNw;
        if (x == 0 && y == MapHeight - 1) return TerrainTiles.WoodsCornerNe;
        return Pick(TerrainTiles.Woods, x, y);
    }

    // ------------------------------------------------------------------
    // Obstacles / spawns / travel helpers
    // ------------------------------------------------------------------

    /// <summary>Blocker cells over a footprint; the gap cell (a doorway whose Door node
    /// carries the blocker instead) is skipped when one is given.</summary>
    protected static void Block(TileMapLayer layer, int x0, int y0, int x1, int y1,
        int gapX = -1, int gapY = -1)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (x == gapX && y == gapY)
                    continue;
                layer.SetCell(new Vector2I(x, y), 0, TerrainTiles.Blocker);
            }
        }
    }

    protected static Marker2D SpawnMarker(string name, int x, int y) =>
        new() { Name = name, Position = new Vector2(x * TileSize + 8, y * TileSize + 8) };

    /// <summary>A walk-on exit covering a road mouth's cells, top-left cell (x, y).
    /// Always enabled — leaving any frame of this town is never gated.</summary>
    protected void AddRoadExit(string name, string targetMapId, string targetSpawnId,
        int x, int y, int widthTiles = 1, int heightTiles = 2)
    {
        var size = new Vector2(widthTiles * TileSize, heightTiles * TileSize);
        var exit = new MapExit
        {
            Name = name,
            TargetMapId = targetMapId,
            TargetSpawnId = targetSpawnId,
            Position = new Vector2(x * TileSize, y * TileSize) + size / 2f,
        };
        exit.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = size },
        });
        AddChild(exit);
    }
}
