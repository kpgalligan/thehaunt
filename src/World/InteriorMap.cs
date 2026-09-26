using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Base for every interior, and THE interior build (<see cref="_Ready"/>, a template no
/// subclass overrides) — the interior twin of <see cref="ExteriorMap"/>'s. Every interior
/// is a Tiled map: read <c>data/maps/(MapId).tmx</c> (an interior-format file: a floor
/// grid, a walls grid and a dressing grid BY NAME, plus the placements) — or, when there
/// is none, the code seed (<see cref="BuildDefaultLayout"/> + <see cref="BuildDefaultRecipe"/>)
/// — resolve every placement up front, then paint, block and place. The room's size is
/// its file's. A subclass supplies only its seed and, where it needs them, the hooks
/// (<see cref="TakesKind"/>, <see cref="ResolveTaken"/>, <see cref="OnBuilt"/>).
///
/// The shell is unchanged from the procedural placeholder these rooms shipped as: the
/// oversized near-black <c>Surround</c> behind Ground (never in the file), a floor, a
/// single-thickness wall ring with each Door flush in it. Walls and fixtures are painted
/// on the Obstacles layer, which is both the visual and the collision; sprite-drawn
/// furniture takes a transparent blocker on the same layer, exactly as the town's facades
/// do. What the file names, the build DERIVES the tiles of: a floor's variant
/// (<c>(x + y) % n</c>), a counter's or hearth's L/C/R piece from its same-name
/// neighbours, and door_open on every door cell with the threshold just inside it. Every
/// painted cell goes through <see cref="InteriorTiles.ForAct"/>.
///
/// Layer order: Surround, Ground, Obstacles, Dressing. Dressing exists for the one tile
/// in the sheet with an alpha channel — the cobweb, which is composited OVER whatever it
/// hangs on rather than replacing it.
/// </summary>
public abstract partial class InteriorMap : MapRoot
{
    /// <summary>
    /// What a floor cell IS, painted on Ground. APPEND-ONLY: the Tiled floors palette
    /// (<see cref="TiledPalette.Floors"/>) indexes tiles by position here. Variants step
    /// <c>(x + y) % n</c> — the reference rooms' diagonal stagger, not a hash:
    /// Plank [plank a, b]; PlankStagger [plank a, b, worn] (the farmhouse); PlankWorn
    /// [worn, plank a, worn] (Billie's); Stone, Board [a, b]; Dirt [dirt];
    /// Check [check a, b]; Hay, RugA, RugB, Stain and Dark one tile each.
    /// </summary>
    public enum Floor { Plank, PlankStagger, PlankWorn, Stone, Dirt, Board, Check, Hay, RugA, RugB, Stain, Dark }

    /// <summary>
    /// What stands on a cell, painted on Obstacles (the visual AND the collision).
    /// APPEND-ONLY: the Tiled walls palette (<see cref="TiledPalette.Walls"/>) indexes
    /// tiles by position here. Each is the same-named <see cref="InteriorTiles"/> tile
    /// (Wall-prefixed there for Plaster, Plank, Stone, Log, PlasterCrack, StoneCrack,
    /// CornerL, CornerR, Beam and Rail), except: Ceiling steps its two tiles
    /// <c>(x + y) % 2</c>; Counter and Hearth pick their L/C/R piece from same-name
    /// neighbours (none west: L; else none east: R; else C); Blocker draws nothing and
    /// only blocks (collision for a sprite). door_open is never named — it is derived on
    /// every door cell.
    ///
    /// The ring's north row is a cornice, painted rather than derived: its material is
    /// picked per room for contrast against THAT room's floor, while the sides carry the
    /// building's material. A log cornice over a plank floor, or a plank one over dirt, is
    /// the same brown twice and the back wall dissolves into the ground.
    /// </summary>
    public enum Wall
    {
        Plaster, Plank, Stone, Log, WainscotPlaster, WainscotPlank, PlasterCrack, StoneCrack, CornerL, CornerR,
        WindowDark, WindowLit, WindowShut, DoorClosed, Beam, CornicePlaster, CornicePlank, CorniceStone, CorniceLog,
        Ceiling, RafterH, RafterV, HayloftEdge, Rail, UpperPlaster, UpperPlank, UpperStone, UpperLog, Plaque,
        LanternBracket, StairUp, StairDown, Hearth, HearthFire, Counter, ShelfEmpty, ShelfFull, Barrel, Crate,
        Sack, HayBale, Blocker,
    }

    /// <summary>What hangs over a cell, on the Dressing layer. APPEND-ONLY, like the others.</summary>
    public enum Dressing { Cobweb }

    /// <summary>Every <see cref="Floor"/> by name, in declaration order — the floors palette.</summary>
    internal static readonly IReadOnlyList<string> FloorNames = Enum.GetNames<Floor>();

    /// <summary>Every <see cref="Wall"/> by name, in declaration order — the walls palette.</summary>
    internal static readonly IReadOnlyList<string> WallNames = Enum.GetNames<Wall>();

    /// <summary>Every <see cref="Dressing"/> by name, in declaration order — the dressing palette.</summary>
    internal static readonly IReadOnlyList<string> DressingNames = Enum.GetNames<Dressing>();

    private static readonly Vector2I[] PlankStagger =
        { InteriorTiles.FloorPlank[0], InteriorTiles.FloorPlank[1], InteriorTiles.FloorPlankWorn };

    private static readonly Vector2I[] PlankWorn =
        { InteriorTiles.FloorPlankWorn, InteriorTiles.FloorPlank[0], InteriorTiles.FloorPlankWorn };

    private static readonly Vector2I[] Check = { InteriorTiles.FloorCheckA, InteriorTiles.FloorCheckB };

    /// <summary>Indoors: fixed warm key, never the day/night tint.</summary>
    public sealed override bool IsInterior => true;

    /// <summary>
    /// Where this build's grids and placements came from: the Tiled file's path, or
    /// <see cref="TestMap.CodeDefaults"/> when there was no file to read. Provenance only.
    /// </summary>
    public string RecipeSource { get; private set; } = "";

    private Floor[,] _floors = new Floor[0, 0];
    private Wall?[,] _walls = new Wall?[0, 0];
    private Dressing?[,] _dressings = new Dressing?[0, 0];

    private TileMapLayer _ground = null!;
    private TileMapLayer _obstacles = null!;
    private TileMapLayer _dressingLayer = null!;

    private int Width => _floors.GetLength(0);
    private int Height => _floors.GetLength(1);

    // ------------------------------------------------------------------
    // The build — one template for every interior
    // ------------------------------------------------------------------

    /// <summary>The room's grids as the code seed lays them. Starts with
    /// <see cref="ResetLayout"/>; must not depend on being in the tree.</summary>
    protected abstract void BuildDefaultLayout();

    /// <summary>The room's placements as the code seed describes them. Must not depend on
    /// being in the tree (<see cref="CodeSeed"/> calls it on a map that never enters it).</summary>
    protected abstract MapRecipe BuildDefaultRecipe();

    /// <summary>True for a room-only kind this map builds itself (the garage's lift).</summary>
    protected virtual bool TakesKind(string kind) => false;

    /// <summary>Validates the taken placements, throwing <see cref="MapRecipeException"/>
    /// before any tile or node exists.</summary>
    protected virtual void ResolveTaken(IReadOnlyList<MapPlacement> taken) { }

    /// <summary>After everything is built: builds from the taken placements (already
    /// validated), in canonical order. Never validates.</summary>
    protected virtual void OnBuilt(IReadOnlyList<MapPlacement> taken) { }

    public override void _Ready()
    {
        MapRecipe recipe;
        if (TiledMapFile.Load(MapId) is { } tiled)
        {
            tiled.RequireFormat(TiledFormat.Interior);
            RecipeSource = tiled.SourcePath;
            LoadLayout(tiled);
            recipe = tiled.Placements;
        }
        else
        {
            RecipeSource = TestMap.CodeDefaults;
            BuildDefaultLayout();
            recipe = BuildDefaultRecipe();
        }

        // Resolve everything up front: a placement this build cannot honour throws here,
        // in one place, naming the file it came from — before anything is painted.
        List<MapPlacement> spawns = recipe.OfKind(PlacementKinds.Spawn).ToList();
        List<MapPlacement> doors = recipe.OfKind(PlacementKinds.Door).ToList();
        var furniture = recipe.OfKind(PlacementKinds.Furniture)
            .Select(p => (Placement: p, Source: PieceFor(p, p.Id))).ToList();
        var beds = recipe.OfKind(PlacementKinds.Bed)
            .Select(p => (Placement: p, Source: PieceFor(p, p.Id))).ToList();
        var chests = recipe.OfKind(PlacementKinds.Chest)
            .Select(p => (Placement: p, Source: p.Text(PlacementFields.Art) is { Length: > 0 } art ? PieceFor(p, art) : default))
            .ToList();
        List<MapPlacement> counters = recipe.OfKind(PlacementKinds.ShopCounter).ToList();
        foreach (MapPlacement counter in counters)
        {
            if (ShopCatalog.TryGet(counter.Id) == null)
            {
                throw new MapRecipeException(RecipeSource,
                    $"places shop counter '{counter.Id}' at {counter.X},{counter.Y}, but there is no such catalog. Known: {string.Join(", ", ShopCatalog.All.Keys)}.");
            }
        }

        var built = new HashSet<string>
        {
            PlacementKinds.Spawn, PlacementKinds.Door, PlacementKinds.Furniture, PlacementKinds.Bed,
            PlacementKinds.Chest, PlacementKinds.ShopCounter,
        };
        var taken = new List<MapPlacement>();
        foreach (string kind in PlacementKinds.All)
        {
            if (built.Contains(kind))
                continue;
            List<MapPlacement> ofKind = recipe.OfKind(kind).ToList();
            if (ofKind.Count == 0)
                continue;
            if (!TakesKind(kind))
            {
                MapPlacement first = ofKind[0];
                throw new MapRecipeException(RecipeSource,
                    $"places a '{first.Kind}' ('{first.Id}' at {first.X},{first.Y}), which map '{MapId}' does not build.");
            }
            taken.AddRange(ofKind);
        }
        ResolveTaken(taken);

        // Tiles.
        BuildSurround();
        TileSet tileSet = InteriorTerrain.Get();

        _ground = new TileMapLayer { Name = "Ground", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                PaintFloor(x, y, _floors[x, y]);
        // Threshold on the floor cell just inside each door — the one place the eye is
        // told where the room begins (handoff §5).
        foreach (MapPlacement door in doors)
        {
            if (Inside(door.X, door.Y - 1))
                PaintGround(door.X, door.Y - 1, InteriorTiles.Threshold);
        }
        AddChild(_ground);

        _obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_walls[x, y] is not { } wall)
                    continue;
                if (wall == Wall.Blocker)
                    Block(x, y);
                else
                    PaintObstacle(x, y, WallTile(wall, x, y));
            }
        }
        // door_open is the sheet's one walkable wall tile, so it draws the opening without
        // blocking it; the Door node carries the blocker on that cell.
        var doorCells = new HashSet<Vector2I>(doors.Select(door => door.Cell));
        foreach (Vector2I cell in doorCells)
            PaintObstacle(cell.X, cell.Y, InteriorTiles.DoorOpen);
        foreach ((MapPlacement placement, Rect2 source) in furniture)
        {
            if (!placement.Bool(PlacementFields.Blocks, true))
                continue;   // walked over, stood on, or sitting on a counter that already blocks
            for (int i = 0; i < Furniture.Tiles(source); i++)
            {
                if (!doorCells.Contains(new Vector2I(placement.X + i, placement.Y)))
                    Block(placement.X + i, placement.Y);
            }
        }
        AddChild(_obstacles);

        _dressingLayer = new TileMapLayer { Name = "Dressing", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (_dressings[x, y] is { } dressing)
                    PaintDressing(x, y, dressing);
        AddChild(_dressingLayer);

        // Nodes.
        foreach ((MapPlacement placement, Rect2 source) in furniture)
        {
            AddChild(new Prop
            {
                TexturePath = Furniture.TexturePath,
                Source = source,
                Position = Prop.Anchor(placement.X, placement.Y, Furniture.Tiles(source)),
            });
        }

        var spawnHost = new Node2D { Name = "Spawns" };
        foreach (MapPlacement spawn in spawns)
            spawnHost.AddChild(SpawnMarker(spawn.Id, spawn.X, spawn.Y));
        AddChild(spawnHost);

        // The bed stands on its cell and overhangs the one above it.
        foreach ((MapPlacement placement, Rect2 source) in beds)
        {
            AddChild(new Bed
            {
                Name = "Bed",
                ArtSource = source,
                Position = new Vector2(placement.X * TileSize + 8, placement.Y * TileSize),
            });
        }

        // A chest's contents live in GameData.Storages under its id; the art is only a look.
        foreach ((MapPlacement placement, Rect2 source) in chests)
        {
            AddChild(new Chest
            {
                Name = $"Chest_{placement.Id}",
                StorageId = placement.Id,
                ArtSource = source,
                Position = CellCentre(placement),
            });
        }

        // ShopCounter has no sprite and no blocker of its own — the counter tiles are the
        // visual and the collision; the map supplies the shape covering them.
        foreach (MapPlacement placement in counters)
        {
            int w = placement.Int(PlacementFields.Width, 1);
            int h = placement.Int(PlacementFields.Height, 1);
            var counter = new ShopCounter
            {
                Name = $"ShopCounter_{placement.Id}",
                CatalogId = placement.Id,
                Position = new Vector2(placement.X * TileSize + w * 8, placement.Y * TileSize + h * 8),
            };
            counter.AddChild(new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = new Vector2(w * TileSize, h * TileSize) },
            });
            AddChild(counter);
        }

        // Every doorway is painted into the wall ring, so the Door nodes contribute their
        // blocker and their prompt only.
        foreach (MapPlacement placement in doors)
        {
            AddChild(new Door
            {
                Name = $"Door_{placement.Id}",
                TargetMapId = placement.Id,
                TargetSpawnId = placement.Text(PlacementFields.Spawn, "default"),
                DrawPlaceholder = false,
                Position = CellCentre(placement),
            });
        }

        OnBuilt(taken);
    }

    /// <summary>A furniture piece by id, or a MapRecipeException naming the file.</summary>
    private Rect2 PieceFor(MapPlacement placement, string id)
    {
        if (!Furniture.Ids.Contains(id))
        {
            string known = string.Join(", ", Furniture.Ids.OrderBy(k => k, StringComparer.Ordinal));
            throw new MapRecipeException(RecipeSource,
                $"places {placement.Kind} '{placement.Id}' at {placement.X},{placement.Y} as furniture '{id}', which map '{MapId}' does not know. Known: {known}.");
        }
        return Furniture.ByName(id);
    }

    /// <summary>The grids from a file: its size is the room's.</summary>
    private void LoadLayout(TiledMap tiled)
    {
        _floors = new Floor[tiled.Width, tiled.Height];
        _walls = new Wall?[tiled.Width, tiled.Height];
        _dressings = new Dressing?[tiled.Width, tiled.Height];
        for (int y = 0; y < tiled.Height; y++)
        {
            for (int x = 0; x < tiled.Width; x++)
            {
                _floors[x, y] = Named<Floor>(tiled.FloorAt(x, y), tiled, x, y);
                if (tiled.WallAt(x, y) is { } wall)
                    _walls[x, y] = Named<Wall>(wall, tiled, x, y);
                if (tiled.DressingAt(x, y) is { } dressing)
                    _dressings[x, y] = Named<Dressing>(dressing, tiled, x, y);
            }
        }
    }

    private static T Named<T>(string name, TiledMap tiled, int x, int y) where T : struct, Enum =>
        Enum.GetNames<T>().Contains(name) && Enum.TryParse(name, ignoreCase: false, out T value)
            ? value
            : throw new MapRecipeException(tiled.SourcePath,
                $"has {typeof(T).Name.ToLowerInvariant()} '{name}' at ({x},{y}); known: {string.Join(", ", Enum.GetNames<T>())}.");

    /// <summary>The whole code seed as a Tiled map: the default grids plus
    /// <see cref="BuildDefaultRecipe"/>. Safe off the tree — the caller frees the map.</summary>
    internal TiledMap CodeSeed()
    {
        BuildDefaultLayout();
        MapRecipe recipe = BuildDefaultRecipe();
        var floors = new string[Width, Height];
        var walls = new string?[Width, Height];
        var dressings = new string?[Width, Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                floors[x, y] = _floors[x, y].ToString();
                walls[x, y] = _walls[x, y]?.ToString();
                dressings[x, y] = _dressings[x, y]?.ToString();
            }
        }
        return TiledMap.Interior(recipe.MapId, floors, walls, dressings, recipe, TestMap.CodeDefaults);
    }

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

    private bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    // ------------------------------------------------------------------
    // Names -> tiles
    // ------------------------------------------------------------------

    private static Vector2I[] FloorTiles(Floor floor) => floor switch
    {
        Floor.Plank => InteriorTiles.FloorPlank,
        Floor.PlankStagger => PlankStagger,
        Floor.PlankWorn => PlankWorn,
        Floor.Stone => InteriorTiles.FloorStone,
        Floor.Dirt => new[] { InteriorTiles.FloorDirt },
        Floor.Board => InteriorTiles.FloorBoard,
        Floor.Check => Check,
        Floor.Hay => new[] { InteriorTiles.FloorHay },
        Floor.RugA => new[] { InteriorTiles.RugA },
        Floor.RugB => new[] { InteriorTiles.RugB },
        Floor.Stain => new[] { InteriorTiles.FloorStain },
        Floor.Dark => new[] { InteriorTiles.FloorDark },
        _ => throw new ArgumentOutOfRangeException(nameof(floor), floor, null),
    };

    /// <summary>A wall's tile where nothing is derived; for the derived ones, the swatch
    /// (Ceiling's first, the C piece, and Zero for the Blocker, which has no sheet tile).</summary>
    private static Vector2I PlainTile(Wall wall) => wall switch
    {
        Wall.Plaster => InteriorTiles.WallPlaster,
        Wall.Plank => InteriorTiles.WallPlank,
        Wall.Stone => InteriorTiles.WallStone,
        Wall.Log => InteriorTiles.WallLog,
        Wall.WainscotPlaster => InteriorTiles.WainscotPlaster,
        Wall.WainscotPlank => InteriorTiles.WainscotPlank,
        Wall.PlasterCrack => InteriorTiles.WallPlasterCrack,
        Wall.StoneCrack => InteriorTiles.WallStoneCrack,
        Wall.CornerL => InteriorTiles.WallCornerL,
        Wall.CornerR => InteriorTiles.WallCornerR,
        Wall.WindowDark => InteriorTiles.WindowDark,
        Wall.WindowLit => InteriorTiles.WindowLit,
        Wall.WindowShut => InteriorTiles.WindowShut,
        Wall.DoorClosed => InteriorTiles.DoorClosed,
        Wall.Beam => InteriorTiles.WallBeam,
        Wall.CornicePlaster => InteriorTiles.CornicePlaster,
        Wall.CornicePlank => InteriorTiles.CornicePlank,
        Wall.CorniceStone => InteriorTiles.CorniceStone,
        Wall.CorniceLog => InteriorTiles.CorniceLog,
        Wall.Ceiling => InteriorTiles.Ceiling[0],
        Wall.RafterH => InteriorTiles.RafterH,
        Wall.RafterV => InteriorTiles.RafterV,
        Wall.HayloftEdge => InteriorTiles.HayloftEdge,
        Wall.Rail => InteriorTiles.WallRail,
        Wall.UpperPlaster => InteriorTiles.UpperPlaster,
        Wall.UpperPlank => InteriorTiles.UpperPlank,
        Wall.UpperStone => InteriorTiles.UpperStone,
        Wall.UpperLog => InteriorTiles.UpperLog,
        Wall.Plaque => InteriorTiles.Plaque,
        Wall.LanternBracket => InteriorTiles.LanternBracket,
        Wall.StairUp => InteriorTiles.StairUp,
        Wall.StairDown => InteriorTiles.StairDown,
        Wall.Hearth => InteriorTiles.HearthC,
        Wall.HearthFire => InteriorTiles.HearthFire,
        Wall.Counter => InteriorTiles.CounterC,
        Wall.ShelfEmpty => InteriorTiles.ShelfEmpty,
        Wall.ShelfFull => InteriorTiles.ShelfFull,
        Wall.Barrel => InteriorTiles.Barrel,
        Wall.Crate => InteriorTiles.Crate,
        Wall.Sack => InteriorTiles.Sack,
        Wall.HayBale => InteriorTiles.HayBale,
        Wall.Blocker => Vector2I.Zero,
        _ => throw new ArgumentOutOfRangeException(nameof(wall), wall, null),
    };

    /// <summary>A wall cell's sheet tile: Ceiling by stagger, Counter and Hearth by
    /// their same-name neighbours, everything else its own tile.</summary>
    private Vector2I WallTile(Wall wall, int x, int y) => wall switch
    {
        Wall.Ceiling => InteriorTiles.Ceiling[(x + y) % 2],
        Wall.Counter => Piece(wall, x, y, InteriorTiles.CounterL, InteriorTiles.CounterC, InteriorTiles.CounterR),
        Wall.Hearth => Piece(wall, x, y, InteriorTiles.HearthL, InteriorTiles.HearthC, InteriorTiles.HearthR),
        _ => PlainTile(wall),
    };

    /// <summary>Panelled ends, plain middle: none of the same west is L, else none east is R.</summary>
    private Vector2I Piece(Wall wall, int x, int y, Vector2I left, Vector2I centre, Vector2I right)
    {
        if (!Inside(x - 1, y) || _walls[x - 1, y] != wall)
            return left;
        if (!Inside(x + 1, y) || _walls[x + 1, y] != wall)
            return right;
        return centre;
    }

    private static Vector2I DressingTile(Dressing dressing) => dressing switch
    {
        Dressing.Cobweb => InteriorTiles.Cobweb,
        _ => throw new ArgumentOutOfRangeException(nameof(dressing), dressing, null),
    };

    /// <summary>The tile a floor's swatch shows: its first variant.</summary>
    internal static Vector2I SwatchTile(Floor floor) => FloorTiles(floor)[0];

    /// <summary>The tile a wall's swatch shows (the C piece, Ceiling's first; Zero for the Blocker).</summary>
    internal static Vector2I SwatchTile(Wall wall) => PlainTile(wall);

    /// <summary>The tile a dressing's swatch shows.</summary>
    internal static Vector2I SwatchTile(Dressing dressing) => DressingTile(dressing);

    // ------------------------------------------------------------------
    // Seed helpers — the code seed's grids, before anything is painted
    // ------------------------------------------------------------------

    /// <summary>
    /// A <paramref name="width"/> x <paramref name="height"/> room of
    /// <paramref name="floor"/> everywhere, the north row <paramref name="cornice"/> (its
    /// dark top edge reads as ceiling shadow; it runs straight into the corners rather
    /// than using the corner pilasters, which only match a plaster wall) and every other
    /// edge cell <paramref name="lower"/>. Every seed starts here.
    /// </summary>
    protected void ResetLayout(int width, int height, Floor floor, Wall lower, Wall cornice)
    {
        _floors = new Floor[width, height];
        _walls = new Wall?[width, height];
        _dressings = new Dressing?[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _floors[x, y] = floor;
                if (y == 0)
                    _walls[x, y] = cornice;
                else if (x == 0 || x == width - 1 || y == height - 1)
                    _walls[x, y] = lower;
            }
        }
    }

    protected void SetFloor(int x, int y, Floor floor) => _floors[x, y] = floor;

    protected void SetWall(int x, int y, Wall wall) => _walls[x, y] = wall;

    protected void SetDressing(int x, int y, Dressing dressing) => _dressings[x, y] = dressing;

    /// <summary>A run of one wall, <paramref name="x0"/> to <paramref name="x1"/> inclusive —
    /// counters and hearths assemble this way (the build picks their pieces).</summary>
    protected void SetWallRun(int x0, int x1, int y, Wall wall)
    {
        for (int x = x0; x <= x1; x++)
            SetWall(x, y, wall);
    }

    /// <summary>Every cell whose loaded floor is <paramref name="floor"/>.</summary>
    protected IEnumerable<Vector2I> CellsOf(Floor floor)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (_floors[x, y] == floor)
                    yield return new Vector2I(x, y);
    }

    /// <summary>Every cell whose loaded dressing is <paramref name="dressing"/>.</summary>
    protected IEnumerable<Vector2I> CellsOf(Dressing dressing)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (_dressings[x, y] == dressing)
                    yield return new Vector2I(x, y);
    }

    // ------------------------------------------------------------------
    // Live painters — the built layers, for a room's own state
    // ------------------------------------------------------------------

    /// <summary>Repaints a Ground cell with <paramref name="floor"/>'s variant for that cell.</summary>
    protected void PaintFloor(int x, int y, Floor floor)
    {
        Vector2I[] tiles = FloorTiles(floor);
        PaintGround(x, y, tiles[(x + y) % tiles.Length]);
    }

    /// <summary>Hangs <paramref name="dressing"/> over a cell; null takes it down. It has to
    /// go on its own layer: a cobweb replacing a hayloft edge would delete the loft.</summary>
    protected void PaintDressing(int x, int y, Dressing? dressing)
    {
        if (dressing is not { } hung)
        {
            _dressingLayer.EraseCell(new Vector2I(x, y));
            return;
        }
        _dressingLayer.SetCell(new Vector2I(x, y), InteriorTerrain.TileSource,
            InteriorTiles.ForAct(DressingTile(hung), InteriorTiles.Act.One));
    }

    /// <summary>Blocks a cell without drawing anything — collision for a sprite.</summary>
    protected void Block(int x, int y) =>
        _obstacles.SetCell(new Vector2I(x, y), InteriorTerrain.BlockerSource, InteriorTerrain.Blocker);

    private void PaintGround(int x, int y, Vector2I tile) =>
        _ground.SetCell(new Vector2I(x, y), InteriorTerrain.TileSource,
            InteriorTiles.ForAct(tile, InteriorTiles.Act.One));

    /// <summary>Paints a solid sheet tile on Obstacles — the visual AND the collision.</summary>
    private void PaintObstacle(int x, int y, Vector2I tile) =>
        _obstacles.SetCell(new Vector2I(x, y), InteriorTerrain.TileSource,
            InteriorTiles.ForAct(tile, InteriorTiles.Act.One));
}
