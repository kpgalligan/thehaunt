using System.Text;
using Godot;

namespace TheHaunt.World;

/// <summary>
/// The Tiled palette for a surface layer, shared by every Tiled map (the exteriors and
/// the farm): one tile per <see cref="ExteriorMap"/> surface, local tile id = its index in
/// <see cref="Names"/>. Pasture and Path are the farm's; each side refuses the other's
/// entries on load. A cell in a Tiled map says what the ground IS (by name); the tile you
/// paint it with in Tiled is only a swatch, never the tile the game draws — painting
/// stays generative C# (BuildGround + ForAct).
///
/// The <c>.tsx</c> and <c>.png</c> are pure derivations of <see cref="Names"/>, rewritten
/// on every <c>--seed-tiled</c> run and never hand-edited. The loader maps gids through
/// <see cref="Names"/>, never by reading the <c>.tsx</c>.
/// </summary>
public static class TiledSurfaces
{
    /// <summary>The tileset reference as written in a <c>.tmx</c> beside data/maps.</summary>
    public const string TilesetSource = "tiled/surfaces.tsx";

    public const string TilesetPath = "res://data/maps/tiled/surfaces.tsx";
    public const string ImagePath = "res://data/maps/tiled/surfaces.png";

    /// <summary>The tile property naming the surface each palette tile stands for.</summary>
    public const string SurfaceProperty = "surface";

    public const int FirstGid = 1;

    private const int Size = MapRoot.TileSize;

    /// <summary>The palette, in tile-id order. Append-only, like the enum it mirrors.</summary>
    public static IReadOnlyList<string> Names => ExteriorMap.SurfaceNames;

    /// <summary>The palette's <c>.tsx</c> text: one image tileset, one row of swatches.</summary>
    public static string ToTsx() => TsxFor("surfaces", SurfaceProperty, Names);

    /// <summary>
    /// A palette's <c>.tsx</c> text: an image tileset named <paramref name="tilesetName"/>
    /// over <c>(tilesetName).png</c>, one row of swatches, each tile carrying
    /// <paramref name="property"/> = its name. Shared by every Tiled palette.
    /// </summary>
    internal static string TsxFor(string tilesetName, string property, IReadOnlyList<string> names)
    {
        int count = names.Count;
        var text = new StringBuilder();
        text.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        text.Append("<tileset version=\"1.10\" tiledversion=\"1.12.0\" name=\"").Append(tilesetName).Append("\" tilewidth=\"")
            .Append(MapRecipe.Number(Size)).Append("\" tileheight=\"").Append(MapRecipe.Number(Size))
            .Append("\" tilecount=\"").Append(MapRecipe.Number(count))
            .Append("\" columns=\"").Append(MapRecipe.Number(count)).Append("\">\n");
        text.Append(" <image source=\"").Append(tilesetName).Append(".png\" width=\"").Append(MapRecipe.Number(count * Size))
            .Append("\" height=\"").Append(MapRecipe.Number(Size)).Append("\"/>\n");
        for (int i = 0; i < count; i++)
        {
            text.Append(" <tile id=\"").Append(MapRecipe.Number(i)).Append("\">\n");
            text.Append("  <properties>\n");
            text.Append("   <property name=\"").Append(property).Append("\" value=\"")
                .Append(names[i]).Append("\"/>\n");
            text.Append("  </properties>\n");
            text.Append(" </tile>\n");
        }
        text.Append("</tileset>\n");
        return text.ToString();
    }

    /// <summary>
    /// The swatch strip, (Names.Count*16) x 16, RGBA8: each surface's plain tile, blitted
    /// from the atlases the game paints with (source 0, the roadside source, the
    /// landscape source, and the farm atlas copy for the farm's Pasture and Path).
    /// </summary>
    public static Image BuildSwatch()
    {
        var tiles = new List<(int, Vector2I)>();
        foreach (string name in Names)
        {
            tiles.Add(name switch
            {
                "Grass" => (0, TerrainTiles.Grass[0]),
                "Dirt" => (0, TerrainTiles.DirtEdge(false, false, false, false)),
                "Gravel" => (0, TerrainTiles.Gravel[0]),
                "Cobble" => (0, TerrainTiles.Cobble[0]),
                "Woods" => (0, TerrainTiles.Woods[0]),
                "Asphalt" => (RoadsideTerrain.SourceId, RoadsideTiles.Asphalt[0]),
                "Concrete" => (RoadsideTerrain.SourceId, RoadsideTiles.Concrete[0]),
                "Road" => (RoadsideTerrain.SourceId, RoadsideTiles.Road[0]),
                "Water" => (RoadsideTerrain.LandscapeSourceId, LandscapeTiles.Water[0]),
                "DeepWater" => (RoadsideTerrain.LandscapeSourceId, LandscapeTiles.DeepWater[0]),
                "Pasture" => (RoadsideTerrain.FenceSourceId, FarmTiles.Pasture[0]),
                "Path" => (RoadsideTerrain.FenceSourceId, FarmTiles.Path[0]),
                _ => throw new InvalidOperationException(
                    $"No swatch for surface '{name}' — add its case to TiledSurfaces.BuildSwatch."),
            });
        }
        return StripFor(tiles);
    }

    /// <summary>
    /// A swatch strip, (tiles.Count*16) x 16, RGBA8: each (source, coords) tile blitted,
    /// in order, from <see cref="RoadsideTerrain.Get"/> — the set every exterior paints with.
    /// </summary>
    internal static Image StripFor(IReadOnlyList<(int SourceId, Vector2I Coords)> tiles)
    {
        TileSet tileSet = RoadsideTerrain.Get();
        var images = new Dictionary<int, (TileSetAtlasSource Source, Image Image)>();

        (TileSetAtlasSource, Image) Atlas(int sourceId)
        {
            if (images.TryGetValue(sourceId, out var cached))
                return cached;
            var source = (TileSetAtlasSource)tileSet.GetSource(sourceId);
            Image image = source.Texture.GetImage();
            if (image.IsCompressed())
                image.Decompress();
            image.Convert(Image.Format.Rgba8);
            images[sourceId] = (source, image);
            return (source, image);
        }

        var swatch = Image.CreateEmpty(tiles.Count * Size, Size, false, Image.Format.Rgba8);
        for (int i = 0; i < tiles.Count; i++)
        {
            (int sourceId, Vector2I coords) = tiles[i];
            (TileSetAtlasSource source, Image image) = Atlas(sourceId);
            swatch.BlitRect(image, source.GetTileTextureRegion(coords), new Vector2I(i * Size, 0));
        }
        return swatch;
    }
}
