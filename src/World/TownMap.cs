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

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Town;
        base._EnterTree();
    }

    // ------------------------------------------------------------------
    // Placements — the code seed and the prop catalog
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

    protected override MapRecipe BuildDefaultRecipe() => DefaultRecipe();

    /// <summary>The whole code seed as a Tiled map: the default surfaces plus <see cref="DefaultRecipe"/>.</summary>
    public static TiledMap DefaultTiledMap() => TiledSeeds.For(MapIds.Town);

    protected override string? SignTextFor(string signId) => Town.SignTextFor(signId);

    /// <summary>
    /// The town's props: the two facades, the cobra heads, and the plaza dressing off the
    /// prop sheet — every one blocks its base row, the 2x2 well its whole footprint.
    /// </summary>
    protected override IReadOnlyDictionary<string, ExteriorProp> PropCatalog()
    {
        int hallTiles = (int)TownHallSource.Size.X / TileSize;
        int storeTiles = (int)StoreFacade.OpenVariant.Size.X / TileSize;
        var catalog = new Dictionary<string, ExteriorProp>(StringComparer.Ordinal)
        {
            [TownProps.TownHallId] = new(p => BuildTownHall(Prop.Anchor(p.X, p.Y, hallTiles)),
                ExteriorProp.Rows(hallTiles, 6)),
            [TownProps.GeneralStoreId] = new(p => new StoreFacade
            {
                Name = "GeneralStore",
                TexturePath = StoreFacade.StorePath,
                Source = StoreFacade.OpenVariant,
                Position = Prop.Anchor(p.X, p.Y, storeTiles),
            }, ExteriorProp.Rows(storeTiles, 4)),
            [TownProps.StreetLightId] = new(p => new StreetLight
            {
                ArmLeft = p.X > PlazaCentre.X,  // the pair faces the plaza
                Position = Prop.Anchor(p.X, p.Y),
            }, ExteriorProp.Rows(1, 1)),
        };
        foreach (string id in TownProps.SheetIds)
        {
            Rect2 sheet = TownProps.ByName(id);
            int tiles = (int)sheet.Size.X / TileSize;
            catalog[id] = new ExteriorProp(p => new Prop
            {
                TexturePath = TownProps.TexturePath,
                Source = sheet,
                Position = Prop.Anchor(p.X, p.Y, tiles),
            }, ExteriorProp.Rows(tiles, id == TownProps.WellId ? 2 : 1));  // the 2x2 well is solid throughout
        }
        return catalog;
    }

    // ------------------------------------------------------------------
    // Surfaces — what each cell IS, before it is any particular tile
    // ------------------------------------------------------------------

    // No longer the shipped ground: Kevin reshaped the woods in Tiled (2026-09-26), so
    // data/maps/town.tmx has left this behind. It is still the fallback when the file is
    // missing, and the seed --seed-tiled writes a fresh file from.
    protected override void BuildDefaultSurfaces()
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
    // Facades (drawn in elevation, anchored on their base row)
    // ------------------------------------------------------------------

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
}
