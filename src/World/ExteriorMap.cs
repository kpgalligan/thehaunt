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
/// Every exterior is a Tiled map, and this class owns the one build (<see cref="_Ready"/>):
/// read <c>data/maps/(MapId).tmx</c> — or, when there is none, the code seed
/// (<see cref="BuildDefaultSurfaces"/> + <see cref="BuildDefaultRecipe"/>) — resolve
/// every placement up front, then paint the ground, block, and place. A subclass
/// supplies only its seed, its prop catalog (<see cref="PropCatalog"/>: placement id →
/// <see cref="ExteriorProp"/>, the node builder plus the cells it blocks) and a few
/// hooks (<see cref="SignTextFor"/>, <see cref="ConfigureDoor"/>, <see cref="ExitGate"/>,
/// <see cref="BuildDressing"/>, <see cref="OnBuilt"/>). Everything painted goes through
/// <see cref="TerrainTiles.ForAct"/> — the act swap is a flag check, never a re-lay.
/// </summary>
public abstract partial class ExteriorMap : MapRoot, ISurfaceGrid
{
    /// <summary>The paved road's two rows, the same in every frame of the strip.</summary>
    protected const int RoadTop = 14, RoadBottom = 15;

    /// <summary>
    /// Asphalt, Concrete and Road paint from the generated roadside source, so a map
    /// that uses them must build its ground with <see cref="RoadsideTerrain.Get"/> —
    /// the plain town set does not carry that source. Road is the town's PAVED
    /// through-road (motel handoff §Road) — a full value-step darker than lot
    /// Asphalt; kerbs, centre line and cracks come from
    /// <see cref="BuildRoadDressing"/>, not the tiles. Dirt remains for the unsealed
    /// roads past the town line (the fork's farm branch, drives, paths).
    ///
    /// Water and DeepWater paint from the generated landscape source
    /// (<see cref="RoadsideTerrain.LandscapeSourceId"/>), so they too need
    /// <see cref="RoadsideTerrain.Get"/>; both are impassable, blocking through their
    /// tiles' own collision. Depth is visual only today — a later fishing hook.
    ///
    /// Pasture and Path are the FARM's (TestMap paints them from its own sheet); they sit
    /// here only because the Tiled palette is one list for every map. An exterior refuses
    /// them on load (<see cref="LoadSurfaces"/>).
    /// </summary>
    protected enum Surface { Grass, Dirt, Gravel, Cobble, Woods, Asphalt, Concrete, Road, Water, DeepWater, Pasture, Path }

    /// <summary>
    /// Every <see cref="Surface"/> by name, in declaration order — the Tiled palette
    /// (<see cref="TiledSurfaces"/>) indexes tiles by position in this list, so the enum
    /// is APPEND-ONLY: reordering or removing a member would repaint every Tiled map.
    /// </summary>
    internal static readonly IReadOnlyList<string> SurfaceNames = Enum.GetNames<Surface>();

    /// <summary>The surfaces an exterior paints: every one but the farm's Pasture and Path.</summary>
    private static readonly IReadOnlyList<string> ExteriorSurfaceNames =
        SurfaceNames.Where(name => name is not (nameof(Surface.Pasture) or nameof(Surface.Path))).ToList();

    /// <summary>
    /// What stands on a cell over its ground, painted on the Obstacles layer: a fence
    /// (the farm sheet's own pieces, picked from its fence neighbours) or a bush. Both
    /// block. APPEND-ONLY, like <see cref="Surface"/>: the Tiled obstacle palette
    /// (<see cref="TiledObstacles"/>) indexes it. Gate is the FARM's (its pen's open
    /// gate); an exterior refuses it on load (<see cref="LoadObstacles"/>).
    /// </summary>
    protected enum Obstacle { Fence, Bush, Gate }

    /// <summary>Every <see cref="Obstacle"/> by name, in declaration order — the Tiled
    /// obstacle palette's tile order.</summary>
    internal static readonly IReadOnlyList<string> ObstacleNames = Enum.GetNames<Obstacle>();

    protected abstract int MapWidth { get; }
    protected abstract int MapHeight { get; }

    protected virtual TerrainTiles.Act CurrentAct => TerrainTiles.Act.One;

    private Surface[,] _surface = new Surface[0, 0];
    private Obstacle?[,] _obstacle = new Obstacle?[0, 0];

    /// <summary>
    /// Where this build's surfaces and placements came from: the Tiled file's path, or
    /// <see cref="TestMap.CodeDefaults"/> when there was no file to read. Provenance only.
    /// </summary>
    public string RecipeSource { get; private set; } = "";

    // ------------------------------------------------------------------
    // The build — one template for every exterior
    // ------------------------------------------------------------------

    /// <summary>The map's ground as the code seed paints it (from <see cref="ResetSurfaces"/>):
    /// what its .tmx was seeded from, and the fallback when the file is missing.</summary>
    protected abstract void BuildDefaultSurfaces();

    /// <summary>The map's placements as the code seed describes them. Must not depend on
    /// being in the tree (<see cref="CodeSeed"/> calls it on a map that never enters it).</summary>
    protected abstract MapRecipe BuildDefaultRecipe();

    /// <summary>Every prop id this map builds: its node builder and the cells it blocks.</summary>
    protected abstract IReadOnlyDictionary<string, ExteriorProp> PropCatalog();

    /// <summary>False for a map with no paved road: no road dressing, and a kerb_cut
    /// placement is refused.</summary>
    protected virtual bool HasRoad => true;

    /// <summary>A promoted sign's copy, by placement id (the place's own
    /// <c>SignTextFor</c>); null leaves the file's <c>text</c>.</summary>
    protected virtual string? SignTextFor(string signId) => null;

    /// <summary>Last word on a door before it enters the tree (a flag lock, a line).</summary>
    protected virtual void ConfigureDoor(Door door) { }

    /// <summary>The gate on an exit, by its id (the target map); null = always open.</summary>
    protected virtual Func<bool>? ExitGate(string exitId) => null;

    /// <summary>Flat ground decals, as children of <paramref name="ground"/>, before the road dressing.</summary>
    protected virtual void BuildDressing(TileMapLayer ground) { }

    /// <summary>After everything is built — for a map that keeps hold of its own nodes.</summary>
    protected virtual void OnBuilt() { }

    public override void _Ready()
    {
        MapRecipe recipe;
        if (TiledMapFile.Load(MapId) is { } tiled)
        {
            RecipeSource = tiled.SourcePath;
            LoadSurfaces(tiled);
            LoadObstacles(tiled);
            recipe = tiled.Placements;
        }
        else
        {
            RecipeSource = TestMap.CodeDefaults;
            BuildDefaultSurfaces();
            recipe = BuildDefaultRecipe();
        }

        // Resolve everything up front: a placement this build cannot honour throws here,
        // in one place, naming the file it came from.
        IReadOnlyDictionary<string, ExteriorProp> catalog = PropCatalog();
        var props = new List<(MapPlacement Placement, ExteriorProp Prop)>();
        var spawns = new List<MapPlacement>();
        var signs = new List<MapPlacement>();
        var doors = new List<MapPlacement>();
        var exits = new List<MapPlacement>();
        var kerbCuts = new List<MapPlacement>();
        foreach (MapPlacement placement in recipe.Placements)
        {
            if (!placement.IsKnown)
                continue;   // a newer build's kind rides through untouched
            switch (placement.Kind)
            {
                case PlacementKinds.Prop:
                    if (!catalog.TryGetValue(placement.Id, out ExteriorProp? prop))
                    {
                        string known = string.Join(", ", catalog.Keys.OrderBy(k => k, StringComparer.Ordinal));
                        throw new MapRecipeException(RecipeSource,
                            $"places prop '{placement.Id}' at {placement.X},{placement.Y}, which map '{MapId}' does not know. Known: {known}.");
                    }
                    props.Add((placement, prop));
                    break;
                case PlacementKinds.Spawn:
                    spawns.Add(placement);
                    break;
                case PlacementKinds.Sign:
                    signs.Add(placement);
                    break;
                case PlacementKinds.Door:
                    doors.Add(placement);
                    break;
                case PlacementKinds.Exit:
                    exits.Add(placement);
                    break;
                case PlacementKinds.KerbCut:
                    if (!HasRoad)
                    {
                        throw new MapRecipeException(RecipeSource,
                            $"places a kerb cut ('{placement.Id}' at {placement.X},{placement.Y}), but map '{MapId}' has no road.");
                    }
                    if (placement.Y is not (RoadTop or RoadBottom))
                    {
                        throw new MapRecipeException(RecipeSource,
                            $"places kerb cut '{placement.Id}' at {placement.X},{placement.Y}; a kerb cut sits on row {RoadTop} (north kerb) or {RoadBottom} (south kerb).");
                    }
                    kerbCuts.Add(placement);
                    break;
                default:
                    throw new MapRecipeException(RecipeSource,
                        $"places a '{placement.Kind}' ('{placement.Id}' at {placement.X},{placement.Y}), which map '{MapId}' does not build.");
            }
        }

        // Ground, its decals, then the road's kerbs — cut wherever made ground meets the
        // gutter, and wherever the file says a driveway does.
        TileSet tileSet = RoadsideTerrain.Get(); // the road, lot and water need the generated sources
        TileMapLayer ground = BuildGround(tileSet);
        BuildDressing(ground);
        if (HasRoad)
        {
            ground.AddChild(BuildRoadDressing(RoadTop,
                KerbCutRuns(RoadTop - 1, kerbCuts.Where(c => c.Y == RoadTop)),
                KerbCutRuns(RoadBottom + 1, kerbCuts.Where(c => c.Y == RoadBottom))));
        }

        // Obstacles: painted fences and bushes first, so a prop footprint overwrites a
        // painted cell; a doorway's Door node carries its own blocker.
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        PaintObstacles(obstacles);
        var doorCells = new HashSet<Vector2I>(doors.Select(door => door.Cell));
        foreach ((MapPlacement placement, ExteriorProp prop) in props)
        {
            foreach (Rect2I rect in prop.Footprint)
            {
                for (int y = rect.Position.Y; y < rect.End.Y; y++)
                {
                    for (int x = rect.Position.X; x < rect.End.X; x++)
                    {
                        var cell = new Vector2I(placement.X + x, placement.Y + y);
                        if (!doorCells.Contains(cell))
                            obstacles.SetCell(cell, 0, TerrainTiles.Blocker);
                    }
                }
            }
        }
        AddChild(obstacles);

        foreach ((MapPlacement placement, ExteriorProp prop) in props)
            AddChild(prop.Build(placement));

        var spawnHost = new Node2D { Name = "Spawns" };
        foreach (MapPlacement spawn in spawns)
            spawnHost.AddChild(SpawnMarker(spawn.Id, spawn.X, spawn.Y));
        AddChild(spawnHost);

        foreach (MapPlacement sign in signs)
        {
            AddChild(new Sign
            {
                Name = sign.Id,
                Position = CellCentre(sign),
                // False where the art already draws the board (a pole sign's foot).
                DrawPlaceholder = sign.Bool(PlacementFields.Board, true),
                // Words with the place (src/Content/Places); the file's text only for a
                // board that has not been promoted there.
                Message = SignTextFor(sign.Id) ?? sign.Text(PlacementFields.Text),
            });
        }

        foreach (MapPlacement exit in exits)
        {
            AddRoadExit($"Exit_{exit.Id}", exit.Id, exit.Text(PlacementFields.Spawn, "default"),
                exit.X, exit.Y, exit.Int(PlacementFields.Width, 1), exit.Int(PlacementFields.Height, 1),
                ExitGate(exit.Id));
        }

        // Every doorway is drawn into its facade, so the Door nodes contribute their
        // blocker and their prompt only.
        foreach (MapPlacement placement in doors)
        {
            var door = new Door
            {
                Name = $"Door_{placement.Id}",
                TargetMapId = placement.Id,
                TargetSpawnId = placement.Text(PlacementFields.Spawn, "default"),
                DrawPlaceholder = false,
                Position = CellCentre(placement),
            };
            ConfigureDoor(door);
            AddChild(door);
        }

        OnBuilt();
    }

    /// <summary>
    /// The maximal column runs where the kerb breaks along one side of the road: cells in
    /// <paramref name="row"/> (the verge row beside the gutter) whose surface is made
    /// ground — Dirt, Gravel or Cobble — plus every kerb_cut's columns. A paved lot
    /// (Asphalt, Concrete) keeps its kerb; its driveway is a kerb_cut.
    /// </summary>
    private (int First, int Last)[] KerbCutRuns(int row, IEnumerable<MapPlacement> cuts)
    {
        var cut = new bool[MapWidth];
        for (int x = 0; x < MapWidth; x++)
            cut[x] = At(x, row) is Surface.Dirt or Surface.Gravel or Surface.Cobble;
        foreach (MapPlacement placement in cuts)
        {
            int width = placement.Int(PlacementFields.Width, 1);
            for (int x = Math.Max(0, placement.X); x < Math.Min(MapWidth, placement.X + width); x++)
                cut[x] = true;
        }

        var runs = new List<(int, int)>();
        int start = -1;
        for (int x = 0; x <= MapWidth; x++)
        {
            bool on = x < MapWidth && cut[x];
            if (on && start < 0)
                start = x;
            else if (!on && start >= 0)
            {
                runs.Add((start, x - 1));
                start = -1;
            }
        }
        return runs.ToArray();
    }

    /// <summary>The whole code seed as a Tiled map: the default surfaces (and their empty
    /// obstacles) plus <see cref="BuildDefaultRecipe"/>. Safe off the tree — the caller
    /// frees the map.</summary>
    internal TiledMap CodeSeed()
    {
        BuildDefaultSurfaces();
        MapRecipe recipe = BuildDefaultRecipe();
        var surfaces = new string[MapWidth, MapHeight];
        var obstacles = new string?[MapWidth, MapHeight];
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                surfaces[x, y] = _surface[x, y].ToString();
                obstacles[x, y] = _obstacle[x, y]?.ToString();
            }
        }
        return new TiledMap(recipe.MapId, surfaces, obstacles, recipe, TestMap.CodeDefaults);
    }

    /// <summary>The bounding box (tiles) of every cell of <paramref name="surface"/>; null if none.</summary>
    protected Rect2I? BoundsOf(Surface surface)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < _surface.GetLength(1); y++)
        {
            for (int x = 0; x < _surface.GetLength(0); x++)
            {
                if (_surface[x, y] != surface)
                    continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }
        return maxX < 0 ? null : new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // ------------------------------------------------------------------
    // Surfaces — what each cell IS, before it is any particular tile
    // ------------------------------------------------------------------

    /// <summary>Woods ring, grass interior — the diegetic map limit every frame starts from.</summary>
    protected void ResetSurfaces()
    {
        _surface = new Surface[MapWidth, MapHeight];
        _obstacle = new Obstacle?[MapWidth, MapHeight];
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                bool border = x == 0 || x == MapWidth - 1 || y == 0 || y == MapHeight - 1;
                _surface[x, y] = border ? Surface.Woods : Surface.Grass;
            }
        }
    }

    /// <summary>
    /// The grid from a Tiled map's surface layer, by NAME. Throws
    /// <see cref="MapRecipeException"/> naming the map's file when its size is not this
    /// map's size or a cell names a surface this build does not have.
    /// </summary>
    protected void LoadSurfaces(TiledMap tiled)
    {
        tiled.RequireFormat(TiledFormat.Exterior);
        if (tiled.Width != MapWidth || tiled.Height != MapHeight)
        {
            throw new MapRecipeException(tiled.SourcePath,
                $"is {tiled.Width}x{tiled.Height} tiles, but map '{MapId}' is {MapWidth}x{MapHeight}.");
        }

        _surface = new Surface[MapWidth, MapHeight];
        _obstacle = new Obstacle?[MapWidth, MapHeight];
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                string name = tiled.SurfaceAt(x, y);
                if (!Enum.TryParse(name, ignoreCase: false, out Surface surface) || !SurfaceNames.Contains(name))
                {
                    throw new MapRecipeException(tiled.SourcePath,
                        $"has surface '{name}' at ({x},{y}); known: {string.Join(", ", SurfaceNames)}.");
                }
                if (surface is Surface.Pasture or Surface.Path)
                {
                    throw new MapRecipeException(tiled.SourcePath,
                        $"has farm-only surface '{name}' at ({x},{y}); an exterior paints: {string.Join(", ", ExteriorSurfaceNames)}.");
                }
                _surface[x, y] = surface;
            }
        }
    }

    /// <summary>
    /// The obstacle grid from a Tiled map's obstacles layer, by NAME; call after
    /// <see cref="LoadSurfaces"/> (which sized the grid and checked the map's size). An
    /// empty cell stays empty. Throws <see cref="MapRecipeException"/> naming the map's
    /// file when a cell names an obstacle this build does not have.
    /// </summary>
    protected void LoadObstacles(TiledMap tiled)
    {
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                string? name = tiled.ObstacleAt(x, y);
                if (name == null)
                    continue;
                if (!Enum.TryParse(name, ignoreCase: false, out Obstacle obstacle) || !ObstacleNames.Contains(name))
                {
                    throw new MapRecipeException(tiled.SourcePath,
                        $"has obstacle '{name}' at ({x},{y}); known: {string.Join(", ", ObstacleNames)}.");
                }
                if (obstacle == Obstacle.Gate)
                {
                    throw new MapRecipeException(tiled.SourcePath,
                        $"has farm-only obstacle '{name}' at ({x},{y}); an exterior paints: {Obstacle.Fence}, {Obstacle.Bush}.");
                }
                _obstacle[x, y] = obstacle;
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

    public int GridWidth => MapWidth;
    public int GridHeight => MapHeight;
    public string SurfaceName(int x, int y) => _surface[x, y].ToString();

    /// <summary>The kerb cuts <see cref="BuildRoadDressing"/> drew — the road's top row
    /// and the north/south column ranges where a driveway breaks the kerb. Read-only,
    /// for the world dump; null on a map with no paved road.</summary>
    internal (int RoadTop, (int First, int Last)[] North, (int First, int Last)[] South)? KerbCuts
    {
        get;
        private set;
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
                if (_surface[x, y] is Surface.Water or Surface.DeepWater)
                {
                    Vector2I water = Pick(_surface[x, y] == Surface.Water
                        ? LandscapeTiles.Water : LandscapeTiles.DeepWater, x, y);
                    ground.SetCell(new Vector2I(x, y), RoadsideTerrain.LandscapeSourceId,
                        LandscapeTiles.ForAct(water, CurrentAct));
                    continue;
                }

                if (_surface[x, y] is Surface.Asphalt or Surface.Concrete or Surface.Road)
                {
                    Vector2I roadside = _surface[x, y] switch
                    {
                        Surface.Asphalt => Pick(RoadsideTiles.Asphalt, x, y),
                        Surface.Road => Pick(RoadsideTiles.Road, x, y),
                        _ => PaintConcrete(x, y),
                    };
                    ground.SetCell(new Vector2I(x, y), RoadsideTerrain.SourceId,
                        RoadsideTiles.ForAct(roadside, CurrentAct));
                    continue;
                }

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

    /// <summary>The kerb draws itself: a walkway cell with the lot directly below
    /// takes the curb tile ("curb along its south edge", motel handoff).</summary>
    private Vector2I PaintConcrete(int x, int y) =>
        At(x, y + 1) == Surface.Asphalt
            ? RoadsideTiles.ConcreteCurb
            : Pick(RoadsideTiles.Concrete, x, y);

    // ------------------------------------------------------------------
    // Road dressing — the paved road's kerbs, centre line and cracks
    // ------------------------------------------------------------------

    private static readonly Color KerbHighlight = new("b8b5a5");
    private static readonly Color KerbShadow = new("2b241d");
    private static readonly Color CentreLine = new("b8b5a5");
    private static readonly Color RoadCrack = new("171310");

    /// <summary>
    /// The paved road's flat detail (motel handoff §Road and kerb): concrete kerb and
    /// gutter along BOTH edges, a worn dashed centre line, patches and cracks —
    /// drawn once, full width, as a decal child of the Ground layer so it renders
    /// over the road tiles and under everything Y-sorted. Dressing, not terrain:
    /// the act pass regenerates it the way it regenerates the tiles. A cut is a
    /// column range where a driveway breaks the kerb, filled with parking-lot mix.
    /// </summary>
    protected Sprite2D BuildRoadDressing(int roadTop,
        (int First, int Last)[] northCuts, (int First, int Last)[] southCuts)
    {
        KerbCuts = (roadTop, northCuts, southCuts);
        int w = MapWidth * TileSize;
        const int h = 34;  // 1px of verge highlight, two road rows, 1px of far shadow
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        bool InCut((int First, int Last)[] cuts, int x)
        {
            foreach ((int first, int last) in cuts)
                if (x >= first * TileSize && x < (last + 1) * TileSize)
                    return true;
            return false;
        }

        for (int x = 0; x < w; x++)
        {
            // North kerb + gutter (local rows 0-6), kerb-top highlight above it,
            // face shadow on its last row — unless a driveway cuts it.
            if (InCut(northCuts, x))
            {
                for (int y = 0; y <= 6; y++)
                    img.SetPixel(x, y, RoadsideTerrain.LotPixel(RoadsideTerrain.Mottle(11, x, y)));
            }
            else
            {
                img.SetPixel(x, 0, KerbHighlight);
                for (int y = 1; y <= 6; y++)
                    img.SetPixel(x, y, RoadsideTerrain.ConcretePixel(RoadsideTerrain.Mottle(12, x, y)));
                img.SetPixel(x, 6, KerbShadow);
            }

            // South kerb, mirrored (local rows 27-33).
            if (InCut(southCuts, x))
            {
                for (int y = 27; y <= 33; y++)
                    img.SetPixel(x, y, RoadsideTerrain.LotPixel(RoadsideTerrain.Mottle(13, x, y)));
            }
            else
            {
                img.SetPixel(x, 27, KerbShadow);
                for (int y = 28; y <= 32; y++)
                    img.SetPixel(x, y, RoadsideTerrain.ConcretePixel(RoadsideTerrain.Mottle(12, x, y)));
                img.SetPixel(x, 33, KerbHighlight);
            }
        }

        // Worn dashed centre line: 8 on, 8 off, with dashes the plows took.
        for (int x = 0; x + 8 <= w; x += 16)
        {
            if (Hash(x, roadTop) % 4 == 0)
                continue;
            img.FillRect(new Rect2I(x, 16, 8, 2), CentreLine);
        }

        // Patches and cracks, no potholes.
        for (int i = 0; i < 26; i++)
        {
            int cx = Hash(i, 29) % (w - 10);
            int cy = 8 + Hash(i, 31) % 18;
            int len = 2 + Hash(i, 37) % 8;
            img.FillRect(new Rect2I(cx, cy, len, 1), RoadCrack);
        }

        return new Sprite2D
        {
            Name = "RoadDressing",
            Centered = false,
            Position = new Vector2(0, roadTop * TileSize - 1),
            Texture = ImageTexture.CreateFromImage(img),
        };
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

    /// <summary>
    /// Paints every non-empty obstacle cell onto <paramref name="layer"/>; empty cells
    /// are left untouched. A bush is a generated landscape tile; a fence is the farm
    /// sheet's own piece (<see cref="RoadsideTerrain.FenceSourceId"/>), picked by
    /// <see cref="FarmTiles.FenceFor"/> from which in-bounds neighbours are fence —
    /// bushes never join a fence. Both block through their tiles' collision.
    /// </summary>
    protected void PaintObstacles(TileMapLayer layer)
    {
        bool IsFence(int x, int y) =>
            x >= 0 && y >= 0 && x < MapWidth && y < MapHeight && _obstacle[x, y] == Obstacle.Fence;

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                switch (_obstacle[x, y])
                {
                    case Obstacle.Bush:
                        layer.SetCell(new Vector2I(x, y), RoadsideTerrain.LandscapeSourceId,
                            LandscapeTiles.ForAct(Pick(LandscapeTiles.Bush, x, y), CurrentAct));
                        break;
                    case Obstacle.Fence:
                        Vector2I piece = FarmTiles.FenceFor(
                            IsFence(x, y - 1), IsFence(x + 1, y), IsFence(x, y + 1), IsFence(x - 1, y));
                        layer.SetCell(new Vector2I(x, y), RoadsideTerrain.FenceSourceId,
                            FarmTiles.ForAct(piece, FarmTiles.Act.One));
                        break;
                }
            }
        }
    }

    /// <summary>A walk-on exit covering a road mouth's cells, top-left cell (x, y),
    /// enabled while <paramref name="isEnabled"/> says so (null = always). The TOWN-LINE
    /// mouths are never gated; a private drive may be (the east fork's drive-in exit,
    /// through <see cref="ExitGate"/>).</summary>
    protected void AddRoadExit(string name, string targetMapId, string targetSpawnId,
        int x, int y, int widthTiles = 1, int heightTiles = 2, Func<bool>? isEnabled = null)
    {
        var size = new Vector2(widthTiles * TileSize, heightTiles * TileSize);
        var exit = new MapExit
        {
            Name = name,
            TargetMapId = targetMapId,
            TargetSpawnId = targetSpawnId,
            Position = new Vector2(x * TileSize, y * TileSize) + size / 2f,
            IsEnabled = isEnabled,
        };
        exit.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = size },
        });
        AddChild(exit);
    }
}
