using Godot;
using TheHaunt.Content;
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

    /// <summary>The one wrong-shaped paving stone (see <see cref="CobbleField"/>) —
    /// read-only, for the world dump.</summary>
    internal static Vector2I WornCobble => PlazaCentre;

    private const int RoadTop = 14, RoadBottom = 15;
    private const int ApronRow = 12;

    internal const string TownHallPath = "res://assets/sprites/town/building_townhall.png";
    private static readonly Rect2 TownHallSource = new(0, 0, 128, 128);

    // ------------------------------------------------------------------
    // The code seed — the placements as they shipped as C# literals
    // ------------------------------------------------------------------
    // These are no longer WHAT the town is built from; data/maps/town.tmx is. They are
    // what that file was seeded from (TiledSeeds), and the fallback when it is not
    // there. TiledMapTests holds the two to each other.

    // Plaza dressing off the prop sheet, as (placement id, base tile). Every one blocks.
    private static readonly (string Id, int X, int Y)[] PlazaProps =
    {
        (TownProps.WellId, 25, 19),
        (TownProps.BenchAId, 22, 19),
        (TownProps.BenchBId, 25, 21),
        (TownProps.NoticeBoardId, 21, 17),
        (TownProps.PlanterIds[0], 21, ApronRow),
        (TownProps.PlanterIds[1], 26, ApronRow),
        (TownProps.PlanterIds[2], 9, ApronRow),
    };

    // Cobra-head street lights where the fire lanterns stood (motel handoff: the
    // town electrified before it had taste; firelight props are replaced town-wide).
    private static readonly Vector2I[] StreetLights = { new(21, 21), new(27, 21) };

    private const string StoreSignId = "StoreSign";

    // The placements this build is reading, resolved once, up front: an id this build
    // does not know throws here, in one place, naming the file it came from.
    private string _recipeSource = "";
    private readonly List<(MapPlacement Placement, Rect2? Sheet, int Tiles, int Rows)> _props = new();
    private MapPlacement[] _spawns = Array.Empty<MapPlacement>();
    private MapPlacement[] _signs = Array.Empty<MapPlacement>();
    private MapPlacement[] _doors = Array.Empty<MapPlacement>();
    private MapPlacement[] _exits = Array.Empty<MapPlacement>();

    /// <summary>
    /// Where this build's surfaces and placements came from: the Tiled file's path, or
    /// <see cref="TestMap.CodeDefaults"/> when there was no file to read. Provenance only.
    /// </summary>
    public string RecipeSource => _recipeSource;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Town;
        base._EnterTree();
    }

    public override void _Ready()
    {
        // RecipeOverride is ignored: the town is not a JSON-recipe map, and the editor
        // stage hands every map an (empty) one. The preview reads the tmx, read-only.
        MapRecipe recipe;
        if (TiledMapFile.Load(MapIds.Town) is { } tiled)
        {
            _recipeSource = tiled.SourcePath;
            LoadSurfaces(tiled);
            LoadObstacles(tiled);
            recipe = tiled.Placements;
        }
        else
        {
            _recipeSource = TestMap.CodeDefaults;
            BuildDefaultSurfaces();
            recipe = DefaultRecipe();
        }
        ResolvePlacements(recipe);

        TileSet tileSet = RoadsideTerrain.Get(); // the paved road needs the roadside source
        TileMapLayer ground = BuildGround(tileSet);
        // Kerb cuts wherever made ground (a door path, the plaza path) meets the gutter.
        ground.AddChild(BuildRoadDressing(RoadTop, KerbCutRuns(RoadTop - 1), KerbCutRuns(RoadBottom + 1)));

        BuildObstacles(tileSet);
        BuildProps();
        BuildSpawns();
        BuildInteractables();
        BuildTravel();
    }

    // ------------------------------------------------------------------
    // Placements — the code seed, and resolving whichever recipe was read
    // ------------------------------------------------------------------

    /// <summary>
    /// The town's placements as the C# literals describe them: the seed
    /// <c>data/maps/town.tmx</c> was written from, and the fallback when it is missing.
    /// </summary>
    public static MapRecipe DefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.Town);

        recipe.Add(PlacementKinds.Prop, TownProps.TownHallId, HallLeft, HallBottom);
        recipe.Add(PlacementKinds.Prop, TownProps.GeneralStoreId, StoreLeft, StoreBottom);
        foreach (var (id, x, y) in PlazaProps)
            recipe.Add(PlacementKinds.Prop, id, x, y);
        foreach (Vector2I coord in StreetLights)
            recipe.Add(PlacementKinds.Prop, TownProps.StreetLightId, coord.X, coord.Y);

        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        recipe.Add(PlacementKinds.Spawn, "from_fork", 2, 15);
        recipe.Add(PlacementKinds.Spawn, "from_east_fork", 45, 15);
        recipe.Add(PlacementKinds.Spawn, "from_hall", DoorX, 13);
        recipe.Add(PlacementKinds.Spawn, "from_store", StoreDoorX, 13);

        // Beside the store's door path; its words live with the place (Town.SignTextFor).
        recipe.Add(PlacementKinds.Sign, StoreSignId, 12, 12);

        // Both doorways are drawn into their facade, so the Door nodes contribute
        // their blocker and their prompt only.
        recipe.Add(PlacementKinds.Door, MapIds.TownHall, DoorX, DoorY)
            .SetText(PlacementFields.Spawn, "entry");
        recipe.Add(PlacementKinds.Door, MapIds.GeneralStore, StoreDoorX, StoreDoorY)
            .SetText(PlacementFields.Spawn, "entry");

        // Road mouths — always enabled: leaving town is never gated.
        foreach ((string target, int x) in new[] { (MapIds.Fork, 0), (MapIds.EastFork, Width - 1) })
        {
            MapPlacement exit = recipe.Add(PlacementKinds.Exit, target, x, RoadTop);
            exit.SetText(PlacementFields.Spawn, "from_town");
            exit.SetInt(PlacementFields.Width, 1);
            exit.SetInt(PlacementFields.Height, 2);
        }

        return recipe;
    }

    /// <summary>The whole code seed as a Tiled map: the default surfaces plus <see cref="DefaultRecipe"/>.</summary>
    public static TiledMap DefaultTiledMap()
    {
        var map = new TownMap();
        try
        {
            map.BuildDefaultSurfaces();
            var surfaces = new string[Width, Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    surfaces[x, y] = map.SurfaceName(x, y);
            return new TiledMap(MapIds.Town, surfaces, DefaultRecipe(), TestMap.CodeDefaults);
        }
        finally
        {
            map.Free();
        }
    }

    private void ResolvePlacements(MapRecipe recipe)
    {
        foreach (MapPlacement placement in recipe.Placements)
        {
            if (!placement.IsKnown)
                continue;   // a newer build's kind rides through untouched
            if (placement.Kind is not (PlacementKinds.Prop or PlacementKinds.Spawn or PlacementKinds.Door
                or PlacementKinds.Exit or PlacementKinds.Sign))
            {
                throw new MapRecipeException(_recipeSource,
                    $"places a '{placement.Kind}' ('{placement.Id}' at {placement.X},{placement.Y}), which the town does not build.");
            }
        }

        _props.Clear();
        foreach (MapPlacement prop in recipe.OfKind(PlacementKinds.Prop))
        {
            switch (prop.Id)
            {
                case TownProps.TownHallId:
                    _props.Add((prop, null, (int)TownHallSource.Size.X / TileSize, 6));
                    break;
                case TownProps.GeneralStoreId:
                    _props.Add((prop, null, (int)StoreFacade.OpenVariant.Size.X / TileSize, 4));
                    break;
                case TownProps.StreetLightId:
                    _props.Add((prop, null, 1, 1));
                    break;
                default:
                    Rect2 sheet;
                    try
                    {
                        sheet = TownProps.ByName(prop.Id);
                    }
                    catch (ArgumentException)
                    {
                        string known = string.Join(", ", new[]
                            { TownProps.TownHallId, TownProps.GeneralStoreId, TownProps.StreetLightId }
                            .Concat(TownProps.SheetIds));
                        throw new MapRecipeException(_recipeSource,
                            $"places prop '{prop.Id}' at {prop.X},{prop.Y}, which the town does not know. Known: {known}.");
                    }
                    // The 2x2 well is solid for its whole footprint, not just its base row.
                    _props.Add((prop, sheet, (int)sheet.Size.X / TileSize, prop.Id == TownProps.WellId ? 2 : 1));
                    break;
            }
        }

        _spawns = recipe.OfKind(PlacementKinds.Spawn).ToArray();
        _signs = recipe.OfKind(PlacementKinds.Sign).ToArray();
        _doors = recipe.OfKind(PlacementKinds.Door).ToArray();
        _exits = recipe.OfKind(PlacementKinds.Exit).ToArray();
    }

    /// <summary>Column runs in one row where made ground (not grass, woods, road or water) meets the kerb.</summary>
    private (int First, int Last)[] KerbCutRuns(int row)
    {
        var runs = new List<(int, int)>();
        int start = -1;
        for (int x = 0; x <= Width; x++)
        {
            bool cut = x < Width && At(x, row) is not (Surface.Grass or Surface.Woods or Surface.Road
                or Surface.Water or Surface.DeepWater);
            if (cut && start < 0)
                start = x;
            else if (!cut && start >= 0)
            {
                runs.Add((start, x - 1));
                start = -1;
            }
        }
        return runs.ToArray();
    }

    // ------------------------------------------------------------------
    // Surfaces — what each cell IS, before it is any particular tile
    // ------------------------------------------------------------------

    // No longer the shipped ground: Kevin reshaped the woods in Tiled (2026-09-26), so
    // data/maps/town.tmx has left this behind. It is still the fallback when the file is
    // missing, and the seed --seed-tiled writes a fresh file from.
    private void BuildDefaultSurfaces()
    {
        ResetSurfaces();

        // Road, continuous with the fork's rows 14-15, open at both mouths — west to
        // the fork, east to the east fork.
        for (int x = 0; x < Width; x++)
        {
            Set(x, RoadTop, Surface.Road);
            Set(x, RoadBottom, Surface.Road);
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
        // Painted fences and bushes first, so a prop footprint overwrites a painted cell.
        PaintObstacles(obstacles);
        var doorCells = new HashSet<Vector2I>(_doors.Select(door => door.Cell));

        foreach (var (prop, _, tiles, rows) in _props)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int i = 0; i < tiles; i++)
                {
                    var cell = new Vector2I(prop.X + i, prop.Y - row);
                    if (!doorCells.Contains(cell))   // a doorway's Door node carries its blocker
                        obstacles.SetCell(cell, 0, TerrainTiles.Blocker);
                }
            }
        }

        AddChild(obstacles);
    }

    // ------------------------------------------------------------------
    // Facades and props (drawn in elevation, anchored on their base row)
    // ------------------------------------------------------------------

    private void BuildProps()
    {
        foreach (var (prop, sheet, tiles, _) in _props)
        {
            Vector2 anchor = Prop.Anchor(prop.X, prop.Y, tiles);
            switch (prop.Id)
            {
                case TownProps.TownHallId:
                    AddChild(BuildTownHall(anchor));
                    break;
                case TownProps.GeneralStoreId:
                    AddChild(new StoreFacade
                    {
                        Name = "GeneralStore",
                        TexturePath = StoreFacade.StorePath,
                        Source = StoreFacade.OpenVariant,
                        Position = anchor,
                    });
                    break;
                case TownProps.StreetLightId:
                    AddChild(new StreetLight
                    {
                        ArmLeft = prop.X > PlazaCentre.X,  // the pair faces the plaza
                        Position = anchor,
                    });
                    break;
                default:
                    AddChild(new Prop
                    {
                        TexturePath = TownProps.TexturePath,
                        Source = sheet!.Value,
                        Position = anchor,
                    });
                    break;
            }
        }
    }

    private static Prop BuildTownHall(Vector2 anchor)
    {
        var hall = new Prop
        {
            Name = "TownHall",
            TexturePath = TownHallPath,
            Source = TownHallSource,
            Position = anchor,
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
        return hall;
    }

    // ------------------------------------------------------------------
    // Spawns / interactables / travel
    // ------------------------------------------------------------------

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        foreach (MapPlacement spawn in _spawns)
            spawns.AddChild(SpawnMarker(spawn.Id, spawn.X, spawn.Y));
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        foreach (MapPlacement sign in _signs)
        {
            AddChild(new Sign
            {
                Name = sign.Id,
                Position = new Vector2(sign.X * TileSize + 8, sign.Y * TileSize + 8),
                // Words with the place (src/Content/Places/Town.cs); the file's text only
                // for a board that has not been promoted there.
                Message = Town.SignTextFor(sign.Id) ?? sign.Text(PlacementFields.Text),
            });
        }
    }

    private void BuildTravel()
    {
        // Road mouths — always enabled (IsEnabled null): leaving town is never gated.
        foreach (MapPlacement exit in _exits)
        {
            AddRoadExit($"Exit_{exit.Id}", exit.Id, exit.Text(PlacementFields.Spawn, "default"),
                exit.X, exit.Y, exit.Int(PlacementFields.Width, 1), exit.Int(PlacementFields.Height, 1));
        }

        // Both doorways are drawn into their facade, so the Door nodes contribute
        // their blocker and their prompt only.
        foreach (MapPlacement door in _doors)
        {
            AddChild(new Door
            {
                Name = $"Door_{door.Id}",
                TargetMapId = door.Id,
                TargetSpawnId = door.Text(PlacementFields.Spawn, "default"),
                DrawPlaceholder = false,
                Position = new Vector2(door.X * TileSize + 8, door.Y * TileSize + 8),
            });
        }
    }
}
