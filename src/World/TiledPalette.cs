using Godot;

namespace TheHaunt.World;

/// <summary>
/// One Tiled palette: a tileset whose tiles stand for NAMES. Local tile id = the name's
/// index in <see cref="Names"/>, so every list here is APPEND-ONLY, like the enum it
/// mirrors. A painted cell says what a cell IS; the swatch is never the tile the game
/// draws — painting stays generative C# (ForAct and the neighbour rules).
///
/// Five palettes, two formats (<see cref="TiledFormat"/>): the exteriors' surfaces and
/// obstacles (<see cref="TiledSurfaces"/>, <see cref="TiledObstacles"/>) and the
/// interiors' floors, walls and dressing (<see cref="InteriorMap.Floor"/>,
/// <see cref="InteriorMap.Wall"/>, <see cref="InteriorMap.Dressing"/>). Each one's
/// <c>.tsx</c> and <c>.png</c> are pure derivations of its names, rewritten on every
/// <c>--seed-tiled</c> run and never hand-edited; the loader maps gids through
/// <see cref="Names"/>, never by reading the <c>.tsx</c>.
/// </summary>
public sealed class TiledPalette
{
    public static readonly TiledPalette Surfaces = new(
        "surfaces", TiledSurfaces.SurfaceProperty, () => TiledSurfaces.Names, TiledSurfaces.BuildSwatch);

    public static readonly TiledPalette Obstacles = new(
        "obstacles", TiledObstacles.ObstacleProperty, () => TiledObstacles.Names, TiledObstacles.BuildSwatch);

    public static readonly TiledPalette Floors = new(
        "floors", "floor", () => InteriorMap.FloorNames,
        () => InteriorStrip(Enum.GetValues<InteriorMap.Floor>().Select(floor => InteriorMap.SwatchTile(floor)).ToList()));

    public static readonly TiledPalette Walls = new(
        "walls", "wall", () => InteriorMap.WallNames, BuildWallSwatch);

    public static readonly TiledPalette Dressing = new(
        "dressing", "dressing", () => InteriorMap.DressingNames,
        () => InteriorStrip(Enum.GetValues<InteriorMap.Dressing>().Select(dressing => InteriorMap.SwatchTile(dressing)).ToList()));

    /// <summary>Every palette, in this order.</summary>
    public static IReadOnlyList<TiledPalette> All { get; } = new[] { Surfaces, Obstacles, Floors, Walls, Dressing };

    private const int Size = MapRoot.TileSize;

    /// <summary>The flat swatch standing for the walls palette's Blocker: it draws nothing in game.</summary>
    private static readonly Color BlockerSwatch = new("#d9338099");

    private readonly Func<IReadOnlyList<string>> _names;
    private readonly Func<Image> _swatch;

    private TiledPalette(string name, string property, Func<IReadOnlyList<string>> names, Func<Image> swatch)
    {
        Name = name;
        Property = property;
        _names = names;
        _swatch = swatch;
    }

    /// <summary>The tileset's name, and its file's (<c>(Name).tsx</c>, <c>(Name).png</c>).</summary>
    public string Name { get; }

    /// <summary>The tile property naming what each tile stands for.</summary>
    public string Property { get; }

    /// <summary>The palette, in tile-id order. APPEND-ONLY.</summary>
    public IReadOnlyList<string> Names => _names();

    /// <summary>The tileset reference as written in a <c>.tmx</c> beside data/maps.</summary>
    public string TilesetSource => $"tiled/{Name}.tsx";

    public string TilesetPath => $"{TiledMapFile.SupportFolder}{Name}.tsx";

    public string ImagePath => $"{TiledMapFile.SupportFolder}{Name}.png";

    /// <summary>The palette's <c>.tsx</c> text: one image tileset, one row of swatches.</summary>
    public string ToTsx() => TiledSurfaces.TsxFor(Name, Property, Names);

    /// <summary>The swatch strip, (Names.Count*16) x 16, RGBA8.</summary>
    public Image BuildSwatch() => _swatch();

    private static Image InteriorStrip(IReadOnlyList<Vector2I> coords) =>
        TiledSurfaces.StripFor(InteriorTerrain.Get(),
            coords.Select(tile => (InteriorTerrain.TileSource, tile)).ToList());

    /// <summary>Each wall's piece off the interior sheet; the Blocker's cell a flat fill.</summary>
    private static Image BuildWallSwatch()
    {
        InteriorMap.Wall[] walls = Enum.GetValues<InteriorMap.Wall>();
        Image strip = InteriorStrip(walls.Select(wall => InteriorMap.SwatchTile(wall)).ToList());
        int blocker = Array.IndexOf(walls, InteriorMap.Wall.Blocker);
        strip.FillRect(new Rect2I(blocker * Size, 0, Size, Size), BlockerSwatch);
        return strip;
    }
}
