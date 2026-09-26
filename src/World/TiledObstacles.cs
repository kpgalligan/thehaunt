using Godot;

namespace TheHaunt.World;

/// <summary>
/// The Tiled palette for the optional obstacles layer: one tile per
/// <see cref="ExteriorMap"/> obstacle, local tile id = its index in <see cref="Names"/>.
/// Like <see cref="TiledSurfaces"/>, a painted cell says what stands there (by name); the
/// swatch is never the tile the game draws — fence pieces are picked by code from their
/// neighbours (<see cref="FarmTiles.FenceFor"/>), and both obstacles block.
///
/// The <c>.tsx</c> and <c>.png</c> are pure derivations of <see cref="Names"/>, rewritten
/// on every <c>--seed-tiled</c> run and never hand-edited.
/// </summary>
public static class TiledObstacles
{
    /// <summary>The tileset reference as written in a <c>.tmx</c> beside data/maps.</summary>
    public const string TilesetSource = "tiled/obstacles.tsx";

    /// <summary>The tile property naming the obstacle each palette tile stands for.</summary>
    public const string ObstacleProperty = "obstacle";

    public const string TilesetPath = "res://data/maps/tiled/obstacles.tsx";
    public const string ImagePath = "res://data/maps/tiled/obstacles.png";

    /// <summary>The palette, in tile-id order. Append-only, like the enum it mirrors.</summary>
    public static IReadOnlyList<string> Names => ExteriorMap.ObstacleNames;

    /// <summary>The firstgid the WRITER gives this tileset: straight after the surfaces.
    /// The reader takes whatever firstgid a file declares.</summary>
    public static int FirstGid => TiledSurfaces.FirstGid + TiledSurfaces.Names.Count;

    /// <summary>The palette's <c>.tsx</c> text: one image tileset, one row of swatches.</summary>
    public static string ToTsx() => TiledSurfaces.TsxFor("obstacles", ObstacleProperty, Names);

    /// <summary>
    /// The swatch strip, (Names.Count*16) x 16, RGBA8: a fence post for Fence and the
    /// first bush for Bush, blitted from the atlases the game paints with.
    /// </summary>
    public static Image BuildSwatch()
    {
        var tiles = new List<(int, Vector2I)>();
        foreach (string name in Names)
        {
            tiles.Add(name switch
            {
                "Fence" => (RoadsideTerrain.FenceSourceId, FarmTiles.FencePost),
                "Bush" => (RoadsideTerrain.LandscapeSourceId, LandscapeTiles.Bush[0]),
                _ => throw new InvalidOperationException(
                    $"No swatch for obstacle '{name}' — add its case to TiledObstacles.BuildSwatch."),
            });
        }
        return TiledSurfaces.StripFor(tiles);
    }
}
