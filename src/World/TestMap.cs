using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The farm, 40x30 tiles, painted from the farm art handoff's sheets
/// (docs/designs/design_handoff_farm_interiors). Pasture — rougher and darker than the
/// town's grass, so the two maps never read as the same field — with a woods edge for the
/// map limit, a track from the farmhouse door east to the wagon road — which bends south
/// at the yard's east end and leaves through the south treeline for the fork (the farm
/// sits NORTH of the fork; docs/story/README.md) — the barn across the yard, and a
/// fenced pen in the south-west.
///
/// Layer child order = draw order: Ground, FarmSoil, Crops, Obstacles. Ground carries
/// everything flat AND the tiles that block by themselves (woods, rocks, stumps, fences);
/// Obstacles carries the transparent blocker under every sprite-drawn structure plus the
/// storm debris. The Crops layer is Y-sorted, because a bean pole is two tiles tall and
/// has to sort against its neighbours and against the player.
///
/// Every gameplay coordinate the intro and the tests depend on is unchanged, with one
/// exception: the farmhouse door moved one tile east, to the column the drawn facade
/// actually puts its door in.
///
/// The PLACEMENTS — scatter, trees, spawns, doors, signs, the bin and the road exit —
/// are read from <c>data/maps/test_farm.json</c> (<see cref="LoadPlacements"/>), which is
/// what makes them draggable. Everything else is still solved in code and stays that way:
/// the surface pass, the road and its kerbs, the pen, the two facades and their footprint
/// blockers, the soil autotile and the scatter hash. A recipe is a build function's
/// INPUT; this is still a build function.
/// </summary>
public partial class TestMap : MapRoot
{
    private const int Width = 40;
    private const int Height = 30;
    private const int BorderThickness = 2;

    private const FarmTiles.Act CurrentAct = FarmTiles.Act.One;

    // Farmhouse: the 6x6 facade overhangs its top two rows, so the footprint is x4-9,
    // y4-7. The door sits in the facade's local column 3 — one tile east of where the
    // procedural placeholder's gap was.
    private const int HouseLeft = 4, HouseRight = 9, HouseBottom = 7;
    private const int HouseTop = HouseBottom - FarmBuildings.FarmhouseFootprintRows + 1;
    private const int HouseDoorX = HouseLeft + FarmBuildings.FarmhouseDoorColumn;   // 7
    private const int HouseDoorY = HouseBottom;                                     // 7

    // Barn: 6x7 facade, bottom five rows are the footprint (handoff §4). Its drawn
    // double door straddles the middle two columns, so the approach is two tiles wide
    // and the blocker gap is the western of the pair — the town hall's precedent.
    private const int BarnLeft = 25, BarnRight = 30, BarnBottom = 9;
    private const int BarnTop = BarnBottom - BarnFacade.FootprintRows + 1;
    private const int BarnDoorX = 27, BarnDoorY = BarnBottom;                       // (27, 9)

    /// <summary>Rows of roof each facade overhangs above its footprint.</summary>
    private const int RoofRows = 2;

    private const int RoadTop = 14, RoadBottom = 15;
    private const int RoadWest = 25;          // west of this the road is a farm track

    // The southbound leg: at the yard's east end the wagon road turns and runs out
    // through the south border toward the fork's north mouth.
    private const int SouthRoadLeft = 36, SouthRoadRight = 37;

    // The fenced pen. Solid rails all round bar the gate, which is drawn and painted open.
    private const int PenLeft = 4, PenRight = 15, PenTop = 23, PenBottom = 26;
    private const int PenGateX = 9;

    private enum Surface { Pasture, Path, Road, Woods }

    private Surface[,] _surface = new Surface[Width, Height];

    /// <summary>The label <see cref="RecipeSource"/> carries when there was no recipe to read.</summary>
    public const string CodeDefaults = "<code defaults>";

    /// <summary>The blockade sign's placement id, which is also its node name.</summary>
    private const string BlockadeSignId = "BlockadeSign";

    // ------------------------------------------------------------------
    // The code seed — the placements as they shipped as C# literals
    // ------------------------------------------------------------------
    // These are no longer WHAT the map is built from; data/maps/test_farm.json is. They
    // are what that file was written from (MapRecipeSeeds), and the fallback for when it
    // is not there. MapSeedTests holds the two to each other, so editing one without the
    // other fails loudly instead of leaving a farm that disagrees with its own file.

    // Rocks and stumps: the same twelve cells the procedural map scattered, moved off the
    // barn, the pen and the one tree canopy that would have buried a boulder — a solid
    // cell whose cause is hidden under leaves is an invisible wall. Solid by the sheet's
    // own collision, so they refuse tilling through the walkable check with nothing
    // hand-listed.
    private static readonly Vector2I[] DefaultBoulders =
    {
        new(5, 12), new(15, 20), new(31, 10), new(25, 22), new(10, 18), new(33, 25),
        new(18, 6), new(28, 19), new(3, 20), new(35, 7), new(22, 3), new(13, 13),
    };

    private static readonly Vector2I[] DefaultFallenLogs = { new(17, 25) };

    // (left column, base row, bare?) — a tree is 3 tiles wide and 4 tall, and only its
    // trunk cell is ever solid: the player walks under the branches. Exactly one bare
    // tree on the whole map: the handoff asks for it sparingly and the reference layout
    // uses it once.
    private static readonly (int X, int Y, bool Bare)[] DefaultTrees =
    {
        (2, 12, false), (16, 9, false), (12, 5, true), (33, 12, false), (21, 20, false),
        (30, 22, false), (5, 19, false), (33, 20, false), (25, 26, false),
    };

    // Storm blockade cells: fallen timber and rock until intro.road_cleared, toggled in
    // ApplyState. Both tiles are solid in the farm sheet, so the debris blocks itself.
    // NOT a placement and deliberately still a literal: these four cells exist only while
    // a story flag is unset, and a recipe record that appears and disappears would need a
    // whole conditional-placement idea the format does not have and should not grow for
    // one blockade.
    private static readonly (Vector2I Cell, Vector2I Tile)[] RoadBlock =
    {
        (new(36, 26), FarmTiles.Log), (new(37, 26), FarmTiles.RockLarge),
        (new(36, 27), FarmTiles.RockLarge), (new(37, 27), FarmTiles.Log),
    };

    // The recipe this build is reading, and the two tables resolved out of it. Resolved
    // once, in one place, because two builders read each table and an id this build does
    // not know has to fail in a single spot if it is going to fail loudly. Same SHAPES
    // the static tables had, so PaintDressing, BuildObstacles' trunk blockers and
    // BuildTrees read exactly what they always read.
    private MapRecipe _recipe = null!;   // LoadPlacements is the first thing _Ready does
    private string _recipeSource = "";
    private (Vector2I Cell, Vector2I Tile)[] _scatter = Array.Empty<(Vector2I, Vector2I)>();
    private (Vector2I Cell, Rect2 Art, Vector2 Nudge)[] _trees =
        Array.Empty<(Vector2I, Rect2, Vector2)>();

    private TileMapLayer? _ground;
    private TileMapLayer? _farmSoil;
    private TileMapLayer? _crops;
    private TileMapLayer? _obstacles;
    // Placed from the recipe, so they are nullable up to the point RequirePlacements has
    // proved both are there — after which _Ready has either thrown or they exist, and
    // every ApplyState read of them is honest.
    private MapExit? _roadExit;
    private Sign? _blockadeSign;
    private BarnFacade _barn = null!;    // built unconditionally in BuildStructures
    private MapState? _pendingState;     // ApplyState arrived before _Ready built the layers

    /// <summary>
    /// Where this build's placements came from: the recipe's path, or
    /// <see cref="CodeDefaults"/> when there was no file to read. Provenance — for the
    /// tests, and for anyone looking at a farm that has lost its trees. Nothing branches
    /// on it and nothing durable lives in it.
    /// </summary>
    public string RecipeSource => _recipeSource;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Farm;
        base._EnterTree();
    }

    public override void _Ready()
    {
        // First, unconditionally: PaintDressing, BuildObstacles and BuildTrees all read
        // tables this fills, and BuildSpawns/BuildInteractables read the recipe itself.
        LoadPlacements();
        BuildSurfaces();
        TileSet tileSet = FarmTerrain.Get();
        BuildGround(tileSet);
        BuildFarmSoil(tileSet);
        BuildCrops();
        BuildObstacles(tileSet);
        BuildStructures();
        BuildTrees();
        BuildSpawns();
        BuildInteractables();
        BuildExits();
        ReserveRoadCorridor();
        ReserveSpriteCover();
        RequirePlacements();

        if (_pendingState is { } pending)
        {
            _pendingState = null;
            ApplyState(pending);
        }
    }

    // ------------------------------------------------------------------
    // Placements — read from data, resolved to art, then handed to the builders
    // ------------------------------------------------------------------

    /// <summary>
    /// Where the farm's scatter, trees, spawns and interactables come from:
    /// <c>data/maps/test_farm.json</c>, read fresh on every build, so an edited recipe
    /// shows up the next time the map loads. Terrain is untouched by any of this — the
    /// surfaces, the road, the pen and the soil autotile stay generative.
    ///
    /// A recipe with NOTHING in it falls back to the code seed. That is the case the
    /// fallback exists for: no file at all — the repo before this one was seeded, and an
    /// export whose non-resource filters forgot <c>data/maps/*.json</c>. It costs nothing,
    /// because a farm with zero placements is not a state anyone means: no doors, no exit,
    /// no spawn to arrive on. The fallback is whole-recipe rather than per-kind on
    /// purpose: this map moved ALL of its placements at once, so a per-kind fallback could
    /// only ever fire on a hand-broken file, where silently resurrecting nine deleted
    /// trees is a worse answer than showing none.
    /// </summary>
    private void LoadPlacements()
    {
        MapRecipe recipe;
        if (RecipeOverride is { } handed)
        {
            // The placement editor is holding the live copy and the file is a drag behind
            // it. Reading disk here would make every rebuild-per-drag undo the drag it was
            // rebuilding for.
            recipe = handed;
            _recipeSource = EditedRecipe;
        }
        else
        {
            recipe = MapRecipeFile.Load(MapIds.Farm);
            _recipeSource = MapRecipeFile.PathFor(MapIds.Farm);
        }
        if (recipe.Placements.Count == 0)
        {
            recipe = DefaultRecipe();
            _recipeSource = CodeDefaults;
        }
        _recipe = recipe;

        // Ids resolve to art HERE and nowhere else, and an id this build does not know
        // throws — naming the id and the ones it does know. An unknown KIND is a newer
        // branch's work and rides through untouched (the format's preserve rule); an
        // unknown ID inside a kind this build BUILDS is a typo in a file someone has open
        // right now, and quietly placing nothing is the worst possible answer to it.
        _scatter = _recipe.OfKind(PlacementKinds.Scatter)
            .Select(cell => (cell.Cell, FarmTiles.ByName(cell.Id)))
            .ToArray();
        _trees = _recipe.OfKind(PlacementKinds.Prop)
            .Select(tree => (tree.Cell, FarmBuildings.TreeArt(tree.Id), tree.Nudge))
            .ToArray();
    }

    /// <summary>
    /// The farm's placements as the C# literals above describe them: the seed
    /// <c>data/maps/test_farm.json</c> was written from, and the fallback when it is
    /// missing. <see cref="MapRecipeSeeds"/> is the door onto it; MapSeedTests pins the
    /// shipped file to it.
    ///
    /// This is the ONLY place the hash that chose between a boulder and a stump still
    /// runs. The recipe records WHICH tile sits on each cell rather than the rule that
    /// picked it, because a rule keyed on the coordinate re-rolls the moment anything
    /// moves the thing one tile east: you would drag a boulder and watch it turn into a
    /// stump. A coordinate is a placement; identity is not derived from one.
    /// </summary>
    public static MapRecipe DefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.Farm);

        foreach (Vector2I cell in DefaultBoulders)
        {
            recipe.Add(PlacementKinds.Scatter,
                Hash(cell.X, cell.Y) % 3 == 0 ? FarmTiles.StumpId : FarmTiles.RockLargeId,
                cell.X, cell.Y);
        }
        foreach (Vector2I cell in DefaultFallenLogs)
        {
            recipe.Add(PlacementKinds.Scatter, FarmTiles.LogId, cell.X, cell.Y);
        }

        foreach (var (x, y, bare) in DefaultTrees)
        {
            recipe.Add(PlacementKinds.Prop,
                bare ? FarmBuildings.TreeBareId : FarmBuildings.TreeLeafyId, x, y);
        }

        recipe.Add(PlacementKinds.Spawn, "default", 20, 15);
        // North of the blockade line, >= 1 tile clear of the exit area
        // (spawn-clearance rule, asserted by test) — and a full tile clear of the
        // debris row, so the feet collider never spawns clipped into it.
        recipe.Add(PlacementKinds.Spawn, "road", 36, 24);
        // One tile south of each door cell — arrival from the building.
        recipe.Add(PlacementKinds.Spawn, "house_door", HouseDoorX, HouseDoorY + 1);
        recipe.Add(PlacementKinds.Spawn, "barn_door", BarnDoorX, BarnDoorY + 1);

        recipe.Add(PlacementKinds.Door, MapIds.FarmHouse, HouseDoorX, HouseDoorY)
            .SetText(PlacementFields.Spawn, "entry");
        recipe.Add(PlacementKinds.Door, MapIds.Barn, BarnDoorX, BarnDoorY)
            .SetText(PlacementFields.Spawn, "entry");

        // The Bed moved indoors with the farmhouse (FarmHouseMap); its old tiles
        // (8,8)/(8,9) are plain tillable pasture now.
        recipe.Add(PlacementKinds.Sign, "Sign", 12, 8)
            .SetText(PlacementFields.Text, "Placeholder sign. Real text comes later.");
        // [KEVIN] placeholder copy — canon restatement only. Beside the southbound
        // leg, one column west of the debris.
        recipe.Add(PlacementKinds.Sign, BlockadeSignId, 35, 26)
            .SetText(PlacementFields.Text,
                "The storm brought half the hillside down. No getting through today.");

        recipe.Add(PlacementKinds.ShippingBin, FarmBuildings.BinId, 10, 8);

        // The south mouth's first border row. One row deep on purpose: the mouth is
        // WIDER than deep, which is how GetArrival knows the cross axis to carry an
        // entering player's position along.
        MapPlacement road = recipe.Add(PlacementKinds.Exit, MapIds.Fork, 36, 28);
        road.SetText(PlacementFields.Spawn, "from_farm");
        road.SetInt(PlacementFields.Width, 2);
        road.SetInt(PlacementFields.Height, 1);

        return recipe;
    }

    /// <summary>
    /// The two placements the farm's story cannot run without. ApplyState toggles both on
    /// every flag change, so a recipe that has lost one would not misbehave subtly — it
    /// would throw a NullReference out of a repaint three beats later, with nothing in the
    /// message about which file to open. Fail here instead, naming the source it came from.
    /// </summary>
    private void RequirePlacements()
    {
        if (_roadExit == null)
        {
            throw new MapRecipeException(_recipeSource,
                $"has no '{PlacementKinds.Exit}' to '{MapIds.Fork}'; the road south is how the farm is left.");
        }
        if (_blockadeSign == null)
        {
            throw new MapRecipeException(_recipeSource,
                $"has no '{PlacementKinds.Sign}' named '{BlockadeSignId}'; the storm blockade needs its sign.");
        }
    }

    /// <summary>Centre of the pixel rect a placement covers from its top-left cell, plus its nudge.</summary>
    private static Vector2 Centre(MapPlacement placement, Vector2 size) =>
        new Vector2(placement.X * TileSize, placement.Y * TileSize) + size / 2f + placement.Nudge;

    /// <summary>Centre of a placement's own tile — where the Area2D interactables sit.</summary>
    private static Vector2 Centre(MapPlacement placement) =>
        Centre(placement, new Vector2(TileSize, TileSize));

    // ------------------------------------------------------------------
    // Surfaces — what each cell IS, before it is any particular tile
    // ------------------------------------------------------------------

    private void BuildSurfaces()
    {
        _surface = new Surface[Width, Height];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool border = x < BorderThickness || x >= Width - BorderThickness
                    || y < BorderThickness || y >= Height - BorderThickness;
                // The woods open where the road leaves for town — through the SOUTH
                // treeline, because the farm sits north of the fork.
                bool roadMouth = y >= Height - BorderThickness
                    && x is >= SouthRoadLeft and <= SouthRoadRight;
                _surface[x, y] = border && !roadMouth ? Surface.Woods : Surface.Pasture;
            }
        }

        // The wagon road, drawn from the same sheet as the road strip's rows 14-15:
        // east along the yard, then the bend — south out of frame, into the fork's
        // north mouth. The map now shows the curve the story always described.
        for (int x = RoadWest; x <= SouthRoadRight; x++)
        {
            _surface[x, RoadTop] = Surface.Road;
            _surface[x, RoadBottom] = Surface.Road;
        }
        Fill(SouthRoadLeft, RoadBottom + 1, SouthRoadRight, Height - 1, Surface.Road);

        // The farm's own track: out of the front door, south to the road line, then east
        // until it meets the road. The player crosses their own yard on every trip to town.
        Fill(HouseDoorX, HouseBottom + 1, HouseDoorX + 1, RoadTop, Surface.Path);
        Fill(HouseDoorX, RoadTop, RoadWest - 1, RoadTop, Surface.Path);

        // Ground under the two facades, plus one apron row, so no grass edge is drawn
        // against a stone foundation the sprite has already painted.
        Fill(HouseLeft, HouseTop, HouseRight, HouseBottom + 1, Surface.Path);
        Fill(BarnLeft, BarnTop, BarnRight, BarnBottom + 1, Surface.Path);

        // Barn approach, two tiles wide under the drawn double door, down to the road.
        Fill(BarnDoorX, BarnBottom + 1, BarnDoorX + 1, RoadTop - 1, Surface.Path);
    }

    private void Fill(int x0, int y0, int x1, int y1, Surface surface)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                _surface[x, y] = surface;
    }

    private Surface At(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Height ? Surface.Woods : _surface[x, y];

    // ------------------------------------------------------------------
    // Ground
    // ------------------------------------------------------------------

    private void BuildGround(TileSet tileSet)
    {
        var ground = new TileMapLayer { Name = "Ground", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_surface[x, y] == Surface.Road || _surface[x, y] == Surface.Woods)
                {
                    // Both come from the town atlas: one road, one forest, one palette.
                    Vector2I town = _surface[x, y] == Surface.Road ? PaintRoad(x, y) : PaintWoods(x, y);
                    ground.SetCell(new Vector2I(x, y), FarmTerrain.TownSource,
                        TerrainTiles.ForAct(town, TerrainTiles.Act.One));
                    continue;
                }
                Vector2I tile = _surface[x, y] == Surface.Path
                    ? Pick(FarmTiles.Path, x, y)
                    : PaintPasture(x, y);
                ground.SetCell(new Vector2I(x, y), FarmTerrain.FarmSource,
                    FarmTiles.ForAct(tile, CurrentAct));
            }
        }

        PaintDressing(ground);
        _ground = ground;
        AddChild(ground);
    }

    private Vector2I PaintPasture(int x, int y)
    {
        // Detail tiles are never adjacent: the checkerboard parity rules out any
        // orthogonal neighbour before the frequency test runs, so the draw is over half
        // the cells and the rates double. Four percent combined, per the handoff —
        // anything past that reads as confetti. dead_grass and flowers are deliberately
        // not scattered: dead_grass is the brightest tile in the sheet and only works in
        // runs or against a path, and flowers are six pixels that vanish at 1x.
        if ((x + y) % 2 == 0)
        {
            int roll = Hash(x, y) % 100;
            if (roll < 5) return Pick(FarmTiles.Weeds, x, y);
            if (roll < 7) return FarmTiles.RockSmall;
            if (roll < 8) return FarmTiles.HayScatter;
        }
        return Pick(FarmTiles.Pasture, x, y);
    }

    // The road's dirt-over-grass set edges wherever it meets anything that is not road;
    // path and pasture both count, so the track reads as the softer surface of the two.
    private Vector2I PaintRoad(int x, int y)
    {
        bool n = At(x, y - 1) != Surface.Road, e = At(x + 1, y) != Surface.Road;
        bool s = At(x, y + 1) != Surface.Road, w = At(x - 1, y) != Surface.Road;
        return n || e || s || w
            ? TerrainTiles.DirtEdge(n, e, s, w)
            : Pick(TerrainTiles.Dirt, x, y);
    }

    private static Vector2I PaintWoods(int x, int y)
    {
        if (x == 0 && y == 0) return TerrainTiles.WoodsCornerSe;
        if (x == Width - 1 && y == 0) return TerrainTiles.WoodsCornerSw;
        if (x == Width - 1 && y == Height - 1) return TerrainTiles.WoodsCornerNw;
        if (x == 0 && y == Height - 1) return TerrainTiles.WoodsCornerNe;
        return Pick(TerrainTiles.Woods, x, y);
    }

    /// <summary>
    /// The solid ground dressing: boulders, stumps, a fallen log, and the pen's rails.
    /// All of it blocks through the sheet's OWN collision, which is also what makes it
    /// refuse tilling — nothing here is hand-listed as unwalkable.
    ///
    /// A scatter placement is a painted CELL, so it lands on the grid and its nudge is
    /// not read: there is no sub-tile position for a tilemap cell to be at. The pen is
    /// not scatter — it is a shape, solved from its corners, and it stays in code.
    /// </summary>
    private void PaintDressing(TileMapLayer ground)
    {
        foreach (var (cell, tile) in _scatter)
            ground.SetCell(cell, FarmTerrain.FarmSource, FarmTiles.ForAct(tile, CurrentAct));

        PaintPen(ground);
    }

    private void PaintPen(TileMapLayer ground)
    {
        void Rail(int x, int y, Vector2I tile) =>
            ground.SetCell(new Vector2I(x, y), FarmTerrain.FarmSource, FarmTiles.ForAct(tile, CurrentAct));

        for (int x = PenLeft + 1; x < PenRight; x++)
        {
            Rail(x, PenTop, FarmTiles.FenceH);
            // The gate is the one cell of the pen that is not solid.
            Rail(x, PenBottom, x == PenGateX ? FarmTiles.GateOpen : FarmTiles.FenceH);
        }
        for (int y = PenTop + 1; y < PenBottom; y++)
        {
            Rail(PenLeft, y, FarmTiles.FenceV);
            Rail(PenRight, y, FarmTiles.FenceV);
        }
        // Corners are named for the two directions their rails run.
        Rail(PenLeft, PenTop, FarmTiles.FenceCornerSe);
        Rail(PenRight, PenTop, FarmTiles.FenceCornerSw);
        Rail(PenLeft, PenBottom, FarmTiles.FenceCornerNe);
        Rail(PenRight, PenBottom, FarmTiles.FenceCornerNw);
    }

    // ------------------------------------------------------------------
    // Obstacles — collision for everything drawn as a sprite, plus the debris
    // ------------------------------------------------------------------

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };

        Block(obstacles, HouseLeft, HouseTop, HouseRight, HouseBottom, HouseDoorX, HouseDoorY);
        Block(obstacles, BarnLeft, BarnTop, BarnRight, BarnBottom, BarnDoorX, BarnDoorY);

        foreach (var (cell, _, _) in _trees)
            Blocker(obstacles, cell.X + FarmBuildings.TreeTrunkColumn, cell.Y);

        _obstacles = obstacles;
        AddChild(obstacles);
    }

    private static void Block(TileMapLayer layer, int x0, int y0, int x1, int y1, int gapX, int gapY)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (x == gapX && y == gapY)
                    continue;   // the Door node carries this cell's blocker
                Blocker(layer, x, y);
            }
        }
    }

    private static void Blocker(TileMapLayer layer, int x, int y) =>
        layer.SetCell(new Vector2I(x, y), FarmTerrain.TownSource, TerrainTiles.Blocker);

    // ------------------------------------------------------------------
    // Structures and trees (drawn in elevation, anchored on their base row)
    // ------------------------------------------------------------------

    private void BuildStructures()
    {
        var house = new Prop
        {
            Name = "Farmhouse",
            TexturePath = FarmBuildings.TexturePath,
            Source = FarmBuildings.Farmhouse,
            Position = Prop.Anchor(HouseLeft, HouseBottom, FarmBuildings.FarmhouseTiles),
        };
        // Offsets are the lit pixels' centres, measured from the facade's bottom-centre.
        // Both windows are drawn lit and the door has a lit transom over it: your own
        // house is the one building in the game that is always expecting you.
        foreach (float windowX in new[] { -27f, 27f })
        {
            house.AddChild(new GlowLight
            {
                Position = new Vector2(windowX, -38f),
                Size = GlowLight.Falloff.Small,
                Strength = 0.55f,
            });
        }
        house.AddChild(new GlowLight
        {
            Name = "Transom",
            Position = new Vector2(8f, -40f),
            Size = GlowLight.Falloff.Small,
            Strength = 0.5f,
        });
        AddChild(house);

        _barn = new BarnFacade
        {
            Name = "Barn",
            TexturePath = BarnFacade.BarnPath,
            Source = BarnFacade.Variant(BarnRules.Derelict),
            Position = Prop.Anchor(BarnLeft, BarnBottom, BarnFacade.Tiles),
        };
        AddChild(_barn);
    }

    private void BuildTrees()
    {
        foreach (var (cell, art, nudge) in _trees)
        {
            AddChild(new Prop
            {
                TexturePath = FarmBuildings.TexturePath,
                Source = art,
                // Both trees' ink stops two rows short of their source rect's bottom
                // edge, so a plain base anchor floats them; drop them back onto the row.
                Position = Prop.Anchor(cell.X, cell.Y, FarmBuildings.TreeTiles)
                    + new Vector2(0, FarmBuildings.TreeInkGap) + nudge,
            });
        }
    }

    // ------------------------------------------------------------------
    // Model -> visual
    // ------------------------------------------------------------------

    /// <summary>
    /// The pure cell-state function: atlas coords each farm layer shows for a tile's
    /// record on a given day (null = empty cell). The soil set is an autotile now, so it
    /// also takes whether each orthogonal neighbour is worked — that is the whole of the
    /// extra input, which is why one tool use repaints exactly five cells.
    /// </summary>
    public static (Vector2I? Soil, Vector2I? Crop) CellState(
        TileRecord? record, long todayIndex,
        bool soilN, bool soilE, bool soilS, bool soilW)
    {
        if (record == null)
            return (null, null);

        Vector2I? soil = null;
        if (record.Kind == "tilled")
        {
            bool wet = record.LastWateredDay == todayIndex;
            soil = !soilN || !soilE || !soilS || !soilW
                ? FarmTiles.SoilEdge(wet, !soilN, !soilE, !soilS, !soilW)
                : FarmTiles.Furrow(wet, soilN, soilE, soilS, soilW)
                  ?? FarmTiles.SoilEdge(wet, false, false, false, false);
        }

        Vector2I? crop = record.CropId == null ? null : CropTiles.Cell(record.CropId, record.GrowthDay);
        return (soil, crop);
    }

    public override bool IsTillable(int x, int y)
    {
        if (_ground == null || _obstacles == null)
            return false;
        var coords = new Vector2I(x, y);
        if (_reservedTiles.Contains(coords))
            return false;
        var tileData = _ground.GetCellTileData(coords);
        if (tileData == null || !tileData.GetCustomData(TileSetTools.WalkableData).AsBool())
            return false;
        return _obstacles.GetCellSourceId(coords) == -1;
    }

    /// <summary>
    /// One tool use changes this cell AND the soil edge of its four neighbours, so the
    /// "O(1) incremental update" is a five-cell plus rather than a single cell.
    /// </summary>
    public override void RefreshTile(int x, int y, TileRecord? record)
    {
        MapState state = SaveService.Instance.Current.GetMap(MapId);
        long today = Clock.Instance.Now.DayIndex;
        PaintTile(state, x, y, today);
        PaintTile(state, x, y - 1, today);
        PaintTile(state, x + 1, y, today);
        PaintTile(state, x, y + 1, today);
        PaintTile(state, x - 1, y, today);
    }

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
        foreach (TileRecord tile in state.Tiles)
            PaintTile(state, tile.X, tile.Y, today);

        // View-side model reads, the same precedent wet/dry soil sets by reading
        // Clock.Now at refresh time. Neither the road nor the barn touches MapState, and
        // every flag-changing path (dawn ordering, SetStoryFlag) repaints here.
        GameData data = SaveService.Instance.Current;

        bool cleared = data.HasFlag(StoryKeys.RoadCleared);
        foreach (var (cell, tile) in RoadBlock)
        {
            if (cleared) _obstacles!.EraseCell(cell);
            else _obstacles!.SetCell(cell, FarmTerrain.FarmSource, FarmTiles.ForAct(tile, CurrentAct));
        }
        // Null-forgiven, not guessed at: this runs only after _Ready, and _Ready either
        // proved both of these exist (RequirePlacements) or threw before the map shipped.
        _roadExit!.SetDeferred(Area2D.PropertyName.Monitoring, cleared);
        // The crew hauls the sign away with the debris — its copy is only true while
        // the road is blocked. SetPresent, not Visible: a hidden Sign keeps colliding.
        _blockadeSign!.SetPresent(!cleared);

        _barn.SetState(BarnRules.StateOf(data));
    }

    private void PaintTile(MapState state, int x, int y, long todayIndex)
    {
        if (_farmSoil == null || _crops == null || x < 0 || y < 0 || x >= Width || y >= Height)
            return;
        var (soil, crop) = CellState(
            state.GetTile(x, y), todayIndex,
            IsWorked(state, x, y - 1), IsWorked(state, x + 1, y),
            IsWorked(state, x, y + 1), IsWorked(state, x - 1, y));

        var coords = new Vector2I(x, y);
        if (soil is { } s)
            _farmSoil.SetCell(coords, FarmTerrain.FarmSource, FarmTiles.ForAct(s, CurrentAct));
        else
            _farmSoil.EraseCell(coords);
        if (crop is { } c)
            _crops.SetCell(coords, 0, c);
        else
            _crops.EraseCell(coords);
    }

    // Planting never clears Kind, so this is true for tilled AND planted cells: a plot
    // stays one continuous piece of worked ground once a crop is in it.
    private static bool IsWorked(MapState state, int x, int y) =>
        state.GetTile(x, y)?.Kind == "tilled";

    // ------------------------------------------------------------------
    // Overlay layers
    // ------------------------------------------------------------------

    private void BuildFarmSoil(TileSet tileSet)
    {
        // The same TileSet as the ground: the soil rows carry no collision, so sharing it
        // costs nothing and keeps one atlas for the whole farm.
        _farmSoil = new TileMapLayer { Name = "FarmSoil", TileSet = tileSet };
        AddChild(_farmSoil);
    }

    private void BuildCrops()
    {
        // Y-sorted: crop cells are 16x32 and overhang the row above, so a bean pole has
        // to sort against its neighbours and against the player walking past it.
        _crops = new TileMapLayer
        {
            Name = "Crops",
            TileSet = CropTiles.Get(),
            YSortEnabled = true,
        };
        AddChild(_crops);
    }

    // ------------------------------------------------------------------
    // Spawns / interactables
    // ------------------------------------------------------------------

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        foreach (MapPlacement spawn in _recipe.OfKind(PlacementKinds.Spawn))
        {
            // The marker's NAME is the id travel asks for (MapRoot.GetSpawn), which is
            // why a spawn's id is the one placement id that is not an art name.
            spawns.AddChild(new Marker2D { Name = spawn.Id, Position = Centre(spawn) });
        }
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        var interactables = new Node2D { Name = "Interactables" };

        foreach (MapPlacement door in _recipe.OfKind(PlacementKinds.Door))
        {
            interactables.AddChild(new Door
            {
                Name = $"Door_{door.Id}",
                TargetMapId = door.Id,
                TargetSpawnId = door.Text(PlacementFields.Spawn, "default"),
                // Both of the farm's doorways are drawn into their facades. A door
                // dropped anywhere else would need the placeholder back — the day that
                // happens it is a field on the record, not a guess made here.
                DrawPlaceholder = false,
                Position = Centre(door),
            });
            // A door cell carries no obstacle blocker (the Door node is the blocker), so
            // without this it would read as open pasture to the hoe.
            _reservedTiles.Add(door.Cell);
        }

        foreach (MapPlacement sign in _recipe.OfKind(PlacementKinds.Sign))
        {
            var post = new Sign
            {
                // A sign's id names the sign, so it is also the node's name — that path
                // is how anything finds one again.
                Name = sign.Id,
                Position = Centre(sign),
                Message = sign.Text(PlacementFields.Text),
            };
            interactables.AddChild(post);
            _reservedTiles.Add(sign.Cell);
            if (sign.Id == BlockadeSignId)
                _blockadeSign = post;
        }

        foreach (MapPlacement bin in _recipe.OfKind(PlacementKinds.ShippingBin))
        {
            var (closed, open) = FarmBuildings.BinArt(bin.Id);
            interactables.AddChild(new ShippingBin
            {
                Name = "ShippingBin",
                ClosedSource = closed,
                OpenSource = open,
                // The drawn bin is two tiles wide and its node sits at the centre of the
                // pair — width taken from the ART, never from a field, so the file and
                // the sheet cannot disagree about how much ground it covers.
                Position = Centre(bin, closed.Size),
            });
            Reserve(bin.X, bin.Y,
                bin.X + Mathf.RoundToInt(closed.Size.X / TileSize) - 1,
                bin.Y + Mathf.RoundToInt(closed.Size.Y / TileSize) - 1);
        }

        AddChild(interactables);
    }

    private void BuildExits()
    {
        foreach (MapPlacement exit in _recipe.OfKind(PlacementKinds.Exit))
        {
            if (_roadExit != null)
            {
                // The farm has one way out by road, and the enabled-until-cleared rule
                // below belongs to THAT exit. A second one would silently inherit it.
                throw new MapRecipeException(_recipeSource,
                    $"has more than one '{PlacementKinds.Exit}'; the farm's only exit is the road south.");
            }

            var size = new Vector2(
                exit.Int(PlacementFields.Width, 1) * TileSize,
                exit.Int(PlacementFields.Height, 1) * TileSize);
            _roadExit = new MapExit
            {
                Name = $"Exit_{exit.Id}",
                TargetMapId = exit.Id,
                TargetSpawnId = exit.Text(PlacementFields.Spawn, "default"),
                Position = Centre(exit, size),
                // Belt (debris collision) and suspenders (disabled exit): even a
                // clipped-through player cannot transition before the road clears.
                IsEnabled = () => SaveService.Instance.Current.HasFlag(StoryKeys.RoadCleared),
            };
            _roadExit.AddChild(new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = size },
            });
            AddChild(_roadExit);
        }
    }

    /// <summary>
    /// Road + blockade cells: never tillable, even once the debris is gone. Not the
    /// exit's footprint and not derived from it — this is where the ROAD is, and the road
    /// is painted generatively, so it is solved from the same constants the surfaces are.
    /// </summary>
    private void ReserveRoadCorridor()
    {
        for (int x = SouthRoadLeft; x <= SouthRoadRight; x++)
            for (int y = 26; y <= Height - 1; y++)
                _reservedTiles.Add(new Vector2I(x, y));
    }

    /// <summary>
    /// Cells a sprite is drawn over but does not block: the two rows of roof each facade
    /// overhangs, and the pen's gateway. The player walks behind them, which is the point
    /// of the Y-sort — but soil hoed there would be painted under the roof and never seen
    /// again, and a mud square in the gate would be the one hole in an otherwise solid
    /// fence. Tree canopies are deliberately NOT reserved: a tree stands south of what it
    /// covers, so the occlusion reads as depth and the player can see what they are
    /// planting under.
    /// </summary>
    private void ReserveSpriteCover()
    {
        Reserve(HouseLeft, HouseTop - RoofRows, HouseRight, HouseTop - 1);
        Reserve(BarnLeft, BarnTop - RoofRows, BarnRight, BarnTop - 1);
        _reservedTiles.Add(new Vector2I(PenGateX, PenBottom));
    }

    private void Reserve(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                _reservedTiles.Add(new Vector2I(x, y));
    }

    // Interactable footprints and the road corridor: tillable terrain, but tilling under
    // a sprite would render invisibly, and the cleared road must stay a road. Populated
    // in BuildInteractables/ReserveRoadCorridor/ReserveSpriteCover so a reservation sits
    // beside the position it covers and the two cannot drift; read only later, by
    // IsTillable, so the order the adders run in does not matter.
    private readonly HashSet<Vector2I> _reservedTiles = new();

    /// <summary>
    /// The farm's reservations, for the placement editor to draw. Handed out live rather
    /// than copied: nothing in the game calls this, and the editor reads it once per
    /// viewport redraw with the map standing still in front of it.
    /// </summary>
    public override IReadOnlyCollection<Vector2I> ReservedTiles() => _reservedTiles;
}
