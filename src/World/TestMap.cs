using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Programmatic placeholder map, 40x30 tiles. Builds its TileSets, layers, spawn
/// markers, and interactables entirely in _Ready — no scene file, no imported assets.
/// Layer child order = draw order: Ground, FarmSoil, Crops, Obstacles.
/// </summary>
public partial class TestMap : MapRoot
{
    private const int Width = 40;
    private const int Height = 30;
    private const int BorderThickness = 2;

    // Ground atlas tile indices (atlas coords (i, 0)).
    private const int GrassA = 0;
    private const int GrassB = 1;
    private const int GrassC = 2;
    private const int Dirt = 3;
    private const int Water = 4;
    private const int Stone = 5;
    private const int Debris = 6; // storm blockade; config identical to Water/Stone
    private const int TileCount = 7;

    // FarmSoil atlas tile indices (atlas coords (i, 0)).
    private const int SoilDry = 0;
    private const int SoilWet = 1;

    private static readonly Color[] TileColors =
    {
        new("4a7c3a"), // grass A
        new("457539"), // grass B
        new("4f823d"), // grass C
        new("8a6a45"), // dirt
        new("3a6ea5"), // water
        new("7a7a7a"), // stone
        new("5a4a2e"), // debris
    };

    private static readonly Color[] SoilColors =
    {
        new("7a5a38"), // tilled, dry
        new("5a4230"), // tilled, wet
    };

    // (28,19) was (28,15) — moved off the road strip when the east road landed.
    private static readonly Vector2I[] StoneCoords =
    {
        new(5, 5), new(15, 20), new(30, 10), new(25, 22), new(10, 18), new(33, 25),
        new(18, 6), new(28, 19), new(6, 24), new(35, 7), new(22, 3), new(13, 13),
    };

    // Storm blockade cells: debris until intro.road_cleared, toggled in ApplyState.
    private static readonly Vector2I[] RoadBlockCells =
    {
        new(36, 14), new(36, 15), new(37, 14), new(37, 15),
    };

    // Crops atlas row per crop id. Row order = CropDefs.All enumeration order
    // (the catalog's insertion order). Both the atlas painter and CellState read
    // this one mapping, so texture and lookup can never disagree.
    private static readonly Dictionary<string, int> CropRows = BuildCropRows();

    private TileMapLayer? _farmSoil;
    private TileMapLayer? _crops;
    private TileMapLayer? _obstacles;
    private MapExit _roadExit = null!; // built in _Ready, before any ApplyState can run
    private Sign _blockadeSign = null!; // same lifecycle as _roadExit
    private MapState? _pendingState; // ApplyState arrived before _Ready built the layers

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = "test_farm";
        base._EnterTree();
    }

    public override void _Ready()
    {
        var tileSet = BuildTileSet();
        BuildGround(tileSet);
        BuildFarmSoil();
        BuildCrops();
        BuildObstacles(tileSet);
        BuildSpawns();
        BuildInteractables();
        BuildRoadExit();

        if (_pendingState is { } pending)
        {
            _pendingState = null;
            ApplyState(pending);
        }
    }

    // ------------------------------------------------------------------
    // Model -> visual
    // ------------------------------------------------------------------

    /// <summary>
    /// The pure cell-state function: atlas coords each farm layer shows for a
    /// tile's record on a given day (null = empty cell). The single source of
    /// truth for both incremental refresh and full repaint.
    /// </summary>
    public static (Vector2I? Soil, Vector2I? Crop) CellState(TileRecord? record, long todayIndex)
    {
        if (record == null)
            return (null, null);

        Vector2I? soil = record.Kind == "tilled"
            ? new Vector2I(record.LastWateredDay == todayIndex ? SoilWet : SoilDry, 0)
            : null;

        Vector2I? crop = null;
        if (record.CropId != null && CropDefs.TryGet(record.CropId) is { } def)
            crop = new Vector2I(def.StageForDay(record.GrowthDay), CropRows[record.CropId]);

        return (soil, crop);
    }

    public override bool IsTillable(int x, int y)
    {
        var ground = Ground;
        if (ground == null || _obstacles == null)
            return false;
        var coords = new Vector2I(x, y);
        if (_reservedTiles.Contains(coords))
            return false;
        var tileData = ground.GetCellTileData(coords);
        if (tileData == null || !tileData.GetCustomData("walkable").AsBool())
            return false;
        return _obstacles.GetCellSourceId(coords) == -1;
    }

    public override void RefreshTile(int x, int y, TileRecord? record) =>
        PaintTile(x, y, record, Clock.Instance.Now.DayIndex);

    public override void ApplyState(MapState state)
    {
        if (_farmSoil == null || _crops == null)
        {
            // Called before _Ready built the layers; _Ready re-applies.
            _pendingState = state;
            return;
        }
        _pendingState = null;
        _farmSoil.Clear();
        _crops.Clear();
        long today = Clock.Instance.Now.DayIndex;
        foreach (var tile in state.Tiles)
            PaintTile(tile.X, tile.Y, tile, today);

        // Road blockade — a view-side model read, same precedent as wet/dry soil
        // reading Clock.Now at refresh time. The road never touches MapState;
        // every flag-changing path (dawn ordering, SetStoryFlag) repaints here.
        bool cleared = SaveService.Instance.Current.HasFlag(StoryKeys.RoadCleared);
        foreach (var c in RoadBlockCells)
            if (cleared) _obstacles!.EraseCell(c);
            else         _obstacles!.SetCell(c, 0, new Vector2I(Debris, 0));
        _roadExit.SetDeferred(Area2D.PropertyName.Monitoring, cleared);
        // The crew hauls the sign away with the debris — its copy is only true while
        // the road is blocked.
        _blockadeSign.Visible = !cleared;
        _blockadeSign.SetDeferred(Area2D.PropertyName.Monitorable, !cleared);
    }

    private void PaintTile(int x, int y, TileRecord? record, long todayIndex)
    {
        if (_farmSoil == null || _crops == null)
            return;
        var (soil, crop) = CellState(record, todayIndex);
        var coords = new Vector2I(x, y);
        if (soil is { } s)
            _farmSoil.SetCell(coords, 0, s);
        else
            _farmSoil.EraseCell(coords);
        if (crop is { } c)
            _crops.SetCell(coords, 0, c);
        else
            _crops.EraseCell(coords);
    }

    // ------------------------------------------------------------------
    // Ground / Obstacles (shared TileSet with physics + walkable data)
    // ------------------------------------------------------------------

    private static TileSet BuildTileSet()
    {
        var ts = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        ts.AddPhysicsLayer();                        // index 0
        ts.SetPhysicsLayerCollisionLayer(0, 1);      // world layer
        ts.SetPhysicsLayerCollisionMask(0, 0);
        ts.AddCustomDataLayer();                     // index 0
        ts.SetCustomDataLayerName(0, "walkable");
        ts.SetCustomDataLayerType(0, Variant.Type.Bool);

        var src = new TileSetAtlasSource
        {
            Texture = BuildAtlasTexture(),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };
        ts.AddSource(src, 0);

        for (int i = 0; i < TileCount; i++)
        {
            var coords = new Vector2I(i, 0);
            src.CreateTile(coords);
            var td = src.GetTileData(coords, 0);
            bool walkable = i is GrassA or GrassB or GrassC or Dirt;
            td.SetCustomData("walkable", walkable);
            if (!walkable)
            {
                td.SetCollisionPolygonsCount(0, 1);
                td.SetCollisionPolygonPoints(0, 0, new[]
                {
                    new Vector2(-8, -8), new Vector2(8, -8), new Vector2(8, 8), new Vector2(-8, 8),
                });
            }
        }

        return ts;
    }

    private static ImageTexture BuildAtlasTexture()
    {
        var img = Image.CreateEmpty(TileCount * TileSize, TileSize, false, Image.Format.Rgba8);
        for (int i = 0; i < TileCount; i++)
        {
            var baseColor = TileColors[i];
            var dark = baseColor.Darkened(0.15f);
            var light = baseColor.Lightened(0.1f);
            for (int py = 0; py < TileSize; py++)
            {
                for (int px = 0; px < TileSize; px++)
                {
                    // Coordinate hash sprinkles a few darker/lighter speckles per tile.
                    int hash = (px * 31 + py * 17 + i * 7) % 23;
                    var color = hash == 0 ? dark : hash == 1 ? light : baseColor;
                    img.SetPixel(i * TileSize + px, py, color);
                }
            }
        }

        // Debris reads as a log pile, not just tinted ground: two log bars with
        // lighter sawn ends and a thin diagonal branch, all deterministic.
        var log = TileColors[Debris].Darkened(0.3f);
        var sawn = TileColors[Debris].Lightened(0.25f);
        img.FillRect(new Rect2I(Debris * TileSize + 1, 4, 14, 3), log);
        img.FillRect(new Rect2I(Debris * TileSize + 1, 4, 2, 3), sawn);
        img.FillRect(new Rect2I(Debris * TileSize + 2, 10, 12, 3), log);
        img.FillRect(new Rect2I(Debris * TileSize + 12, 10, 2, 3), sawn);
        for (int px = 3; px <= 12; px++)
            img.SetPixel(Debris * TileSize + px, 15 - px / 2, log.Lightened(0.1f));

        return ImageTexture.CreateFromImage(img);
    }

    private void BuildGround(TileSet tileSet)
    {
        var ground = new TileMapLayer { Name = "Ground", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool dirt = x >= 18 && x <= 24 && y >= 12 && y <= 17;
                bool road = y is 14 or 15 && x >= 25; // field's east edge to the map edge
                int tile = dirt || road ? Dirt : (x * 7 + y * 13) % 3;
                ground.SetCell(new Vector2I(x, y), 0, new Vector2I(tile, 0));
            }
        }
        AddChild(ground);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool border = x < BorderThickness || x >= Width - BorderThickness
                    || y < BorderThickness || y >= Height - BorderThickness;
                // The water ring opens where the road leaves for town.
                bool roadMouth = x >= 38 && y is 14 or 15;
                if (border && !roadMouth)
                    obstacles.SetCell(new Vector2I(x, y), 0, new Vector2I(Water, 0));
            }
        }
        foreach (var coord in StoneCoords)
            obstacles.SetCell(coord, 0, new Vector2I(Stone, 0));
        _obstacles = obstacles;
        AddChild(obstacles);
    }

    // ------------------------------------------------------------------
    // FarmSoil / Crops (plain overlay TileSets: no physics, no custom data)
    // ------------------------------------------------------------------

    private void BuildFarmSoil()
    {
        var ts = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        var src = new TileSetAtlasSource
        {
            Texture = BuildSoilAtlasTexture(),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };
        ts.AddSource(src, 0);
        src.CreateTile(new Vector2I(SoilDry, 0));
        src.CreateTile(new Vector2I(SoilWet, 0));

        _farmSoil = new TileMapLayer { Name = "FarmSoil", TileSet = ts };
        AddChild(_farmSoil);
    }

    private static ImageTexture BuildSoilAtlasTexture()
    {
        var img = Image.CreateEmpty(SoilColors.Length * TileSize, TileSize, false, Image.Format.Rgba8);
        for (int i = 0; i < SoilColors.Length; i++)
        {
            var baseColor = SoilColors[i];
            var furrow = baseColor.Darkened(0.25f);
            for (int py = 0; py < TileSize; py++)
            {
                for (int px = 0; px < TileSize; px++)
                    img.SetPixel(i * TileSize + px, py, py % 4 == 1 ? furrow : baseColor);
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    private void BuildCrops()
    {
        var ts = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        var src = new TileSetAtlasSource
        {
            Texture = BuildCropAtlasTexture(),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };
        ts.AddSource(src, 0);
        foreach (var def in CropDefs.All.Values)
        {
            int row = CropRows[def.Id];
            for (int col = 0; col <= def.StageDays.Length; col++)
                src.CreateTile(new Vector2I(col, row));
        }

        _crops = new TileMapLayer { Name = "Crops", TileSet = ts };
        AddChild(_crops);
    }

    private static Dictionary<string, int> BuildCropRows()
    {
        var rows = new Dictionary<string, int>();
        foreach (string id in CropDefs.All.Keys)
            rows[id] = rows.Count;
        return rows;
    }

    private static ImageTexture BuildCropAtlasTexture()
    {
        int maxColumns = 1;
        foreach (var def in CropDefs.All.Values)
            maxColumns = Math.Max(maxColumns, def.StageDays.Length + 1);

        var img = Image.CreateEmpty(maxColumns * TileSize, CropRows.Count * TileSize, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        foreach (var def in CropDefs.All.Values)
        {
            int row = CropRows[def.Id];
            var fruit = new Color(ItemDefs.TryGet(def.HarvestItemId)?.IconColor ?? "#d04a9a");
            for (int col = 0; col <= def.StageDays.Length; col++)
                DrawCropTile(img, col * TileSize, row * TileSize, col, def.StageDays.Length, fruit);
        }
        return ImageTexture.CreateFromImage(img);
    }

    // Column 0 = sprout, column stageCount = mature (fruit color pop).
    private static void DrawCropTile(Image img, int ox, int oy, int column, int stageCount, Color fruit)
    {
        var stem = new Color("3a6e2a");
        var leaf = new Color("5aa04a");

        int height = 3 + column * 9 / stageCount;    // 3 px sprout -> 12 px mature
        int top = 15 - height;
        for (int y = top; y <= 14; y++)
        {
            img.SetPixel(ox + 7, oy + y, stem);
            img.SetPixel(ox + 8, oy + y, stem);
        }

        int span = Math.Min(3, 1 + column);          // leaves widen as it grows
        for (int i = 1; i <= span; i++)
        {
            int ly = top + 1 + i;
            if (ly <= 14)
            {
                img.SetPixel(ox + 7 - i, oy + ly, leaf);
                img.SetPixel(ox + 8 + i, oy + ly, leaf);
            }
        }

        if (column == stageCount)
            img.FillRect(new Rect2I(ox + 6, oy + top, 4, 4), fruit);
    }

    // ------------------------------------------------------------------
    // Spawns / interactables
    // ------------------------------------------------------------------

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "default",
            Position = new Vector2(20 * TileSize + 8, 15 * TileSize + 8), // (328, 248)
        });
        spawns.AddChild(new Marker2D
        {
            Name = "road",
            // West of the blockade line, >= 1 tile clear of the exit area
            // (spawn-clearance rule, asserted by test).
            Position = new Vector2(35 * TileSize + 8, 15 * TileSize + 8), // (568, 248)
        });
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        var interactables = new Node2D { Name = "Interactables" };
        interactables.AddChild(new Bed
        {
            Name = "Bed",
            Position = new Vector2(136, 152), // footprint tiles (8,8)-(8,9), position per spec
        });
        _reservedTiles.Add(new Vector2I(8, 8));
        _reservedTiles.Add(new Vector2I(8, 9));
        interactables.AddChild(new Sign
        {
            Name = "Sign",
            Position = new Vector2(200, 136), // tile (12,8) center
            Message = "Placeholder sign. Real text comes later.",
        });
        _reservedTiles.Add(new Vector2I(12, 8));
        interactables.AddChild(new ShippingBin
        {
            Name = "ShippingBin",
            Position = new Vector2(168, 136), // tile (10,8) center
        });
        _reservedTiles.Add(new Vector2I(10, 8));
        _blockadeSign = new Sign
        {
            Name = "BlockadeSign",
            Position = new Vector2(584, 216), // tile (36,13) center, beside the debris
            // [KEVIN] placeholder copy — canon restatement only.
            Message = "The storm brought half the hillside down. No getting through today.",
        };
        interactables.AddChild(_blockadeSign);
        _reservedTiles.Add(new Vector2I(36, 13));
        AddChild(interactables);
    }

    private void BuildRoadExit()
    {
        _roadExit = new MapExit
        {
            Name = "RoadExit",
            TargetMapId = MapIds.Town,
            TargetSpawnId = "from_farm",
            Position = new Vector2(39 * TileSize, 15 * TileSize), // center of tiles (38,14)-(39,15)
            // Belt (debris collision) and suspenders (disabled exit): even a
            // clipped-through player cannot transition before the road clears.
            IsEnabled = () => SaveService.Instance.Current.HasFlag(StoryKeys.RoadCleared),
        };
        _roadExit.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(32, 32) },
        });
        AddChild(_roadExit);

        // Road + blockade cells: never tillable, even once the debris is gone.
        for (int x = 36; x <= 39; x++)
            for (int y = 14; y <= 15; y++)
                _reservedTiles.Add(new Vector2I(x, y));
    }

    // Interactable footprints and the road corridor: tillable terrain, but tilling under
    // a sprite would render invisibly, and the cleared road must stay a road. Populated
    // in BuildInteractables/BuildRoadExit so positions and reservations live side by
    // side and cannot drift.
    private readonly HashSet<Vector2I> _reservedTiles = new();
}
