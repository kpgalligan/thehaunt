namespace TheHaunt.World;

/// <summary>
/// Which tile layers a Tiled map has, each painted from its own palette. The first layer
/// is REQUIRED and has no empty cell; the rest are optional, and read 0 as empty.
///
/// <see cref="Exterior"/> is every <see cref="ExteriorMap"/> and the farm: a surface
/// layer and an obstacles layer. <see cref="Interior"/> is every <see cref="InteriorMap"/>:
/// a floor layer, a walls layer and a dressing layer. A file is one format or the other
/// — its tilesets say which — and a map refuses the other's
/// (<see cref="TiledMap.RequireFormat"/>).
/// </summary>
public sealed class TiledFormat
{
    public static readonly TiledFormat Exterior = new("exterior", new[]
    {
        (TiledMap.SurfaceLayer, TiledPalette.Surfaces),
        (TiledMap.ObstacleLayer, TiledPalette.Obstacles),
    });

    public static readonly TiledFormat Interior = new("interior", new[]
    {
        (TiledMap.FloorLayer, TiledPalette.Floors),
        (TiledMap.WallLayer, TiledPalette.Walls),
        (TiledMap.DressingLayer, TiledPalette.Dressing),
    });

    /// <summary>Both formats.</summary>
    internal static IReadOnlyList<TiledFormat> All { get; } = new[] { Exterior, Interior };

    private TiledFormat(string name, IReadOnlyList<(string Layer, TiledPalette Palette)> layers)
    {
        Name = name;
        Layers = layers;
    }

    public string Name { get; }

    /// <summary>The tile layers, in file order: [0] required, no empty cell; the rest
    /// optional, 0 = empty.</summary>
    public IReadOnlyList<(string Layer, TiledPalette Palette)> Layers { get; }
}
