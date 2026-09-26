using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace TheHaunt.World;

/// <summary>
/// One map as a Tiled TMX file: its tile layers BY NAME, and its placements as rectangle
/// objects. Which tile layers a map has is its <see cref="Format"/>
/// (<see cref="TiledFormat"/>): an EXTERIOR map (every <see cref="ExteriorMap"/> and the
/// farm) has a surface grid (what each cell IS) and an optional obstacle grid (what
/// stands on it; null = empty); an INTERIOR map (every <see cref="InteriorMap"/>) has a
/// floor grid, and optional walls and dressing grids. The tile a person paints with in
/// Tiled is only a swatch from that layer's <see cref="TiledPalette"/>; tile painting
/// stays generative. Span kinds (exit, shop_counter, kerb_cut) carry their "w"/"h" as
/// the rectangle's size, not as properties.
///
/// The reader is a STRICT subset of TMX — the shape the writer emits and Tiled saves back:
/// orthogonal, 16px, finite; external tilesets, each one palette's <c>(Name).tsx</c> at
/// most once and all of ONE format, that format's first palette required; map-sized CSV
/// tile layers named for that format's layers, each at most once, in any order, its
/// first layer required; one object group named "placements" of plain rectangles on the
/// grid. A gid resolves to the tileset with the greatest firstgid at or below it, so a
/// file saved before a palette entry was appended still reads, and it must land in its
/// layer's own palette. The first layer has no empty cell; a missing optional layer means
/// every cell is empty, and it needs no tileset while all its cells are 0. Anything else
/// throws <see cref="MapRecipeException"/> naming the file, because a map half-read is a
/// map quietly missing things. <c>&lt;editorsettings&gt;</c> and unknown attributes are
/// ignored.
///
/// The writer is canonical and deterministic ("\n" endings, tilesets and tile layers in
/// the format's order, objects in MapRecipe's (y, x, kind, id) order, ids 1..N, property
/// keys ordinal-sorted), laid out the way Tiled itself lays a TMX out, so a Tiled save of
/// an untouched map is a near no-op diff.
/// </summary>
public sealed class TiledMap
{
    public const string SurfaceLayer = "surface";
    public const string PlacementLayer = "placements";
    public const string ObstacleLayer = "obstacles";
    public const string FloorLayer = "floor", WallLayer = "walls", DressingLayer = "dressing";
    public const string MapIdProperty = "map";

    private const int Tile = MapRoot.TileSize;
    private const uint FlipMask = 0x0FFFFFFF;

    // One grid per Format.Layers entry, indexed [x, y]; [0] has no null cell.
    private readonly string?[][,] _grids;

    /// <summary>An exterior map with no obstacles.</summary>
    /// <param name="surfaces">Surface names, indexed [x, y].</param>
    public TiledMap(string mapId, string[,] surfaces, MapRecipe placements, string sourcePath)
        : this(mapId, surfaces, new string?[surfaces.GetLength(0), surfaces.GetLength(1)], placements, sourcePath)
    {
    }

    /// <summary>An exterior map.</summary>
    /// <param name="surfaces">Surface names, indexed [x, y].</param>
    /// <param name="obstacles">Obstacle names, indexed [x, y], null = empty; the surfaces' size.</param>
    public TiledMap(string mapId, string[,] surfaces, string?[,] obstacles, MapRecipe placements, string sourcePath)
        : this(TiledFormat.Exterior, mapId, new string?[][,] { surfaces, obstacles }, placements, sourcePath)
    {
    }

    private TiledMap(TiledFormat format, string mapId, string?[][,] grids, MapRecipe placements, string sourcePath)
    {
        for (int i = 1; i < grids.Length; i++)
        {
            if (grids[i].GetLength(0) != grids[0].GetLength(0) || grids[i].GetLength(1) != grids[0].GetLength(1))
            {
                throw new ArgumentException(
                    $"The '{format.Layers[i].Layer}' grid is {grids[i].GetLength(0)}x{grids[i].GetLength(1)}, but " +
                    $"'{format.Layers[0].Layer}' is {grids[0].GetLength(0)}x{grids[0].GetLength(1)}.", nameof(grids));
            }
        }
        Format = format;
        MapId = mapId;
        _grids = grids;
        Placements = placements;
        SourcePath = sourcePath;
    }

    /// <summary>An interior map.</summary>
    /// <param name="floors">Floor names, indexed [x, y].</param>
    /// <param name="walls">Wall names, indexed [x, y], null = empty; the floors' size.</param>
    /// <param name="dressing">Dressing names, indexed [x, y], null = empty; the floors' size.</param>
    public static TiledMap Interior(string mapId, string[,] floors, string?[,] walls, string?[,] dressing,
        MapRecipe placements, string sourcePath) =>
        new(TiledFormat.Interior, mapId, new string?[][,] { floors, walls, dressing }, placements, sourcePath);

    /// <summary>Which tile layers this map has.</summary>
    public TiledFormat Format { get; }

    public string MapId { get; }
    public int Width => _grids[0].GetLength(0);
    public int Height => _grids[0].GetLength(1);

    /// <summary>The file (or label) this map came from — named in every error about it.</summary>
    public string SourcePath { get; }

    public MapRecipe Placements { get; }

    /// <summary>The surface on a cell by name. Exterior maps only.</summary>
    public string SurfaceAt(int x, int y) => Cell(TiledFormat.Exterior, 0, x, y)!;

    /// <summary>The obstacle on a cell by name, or null when it is empty. Exterior maps only.</summary>
    public string? ObstacleAt(int x, int y) => Cell(TiledFormat.Exterior, 1, x, y);

    /// <summary>The floor on a cell by name. Interior maps only.</summary>
    public string FloorAt(int x, int y) => Cell(TiledFormat.Interior, 0, x, y)!;

    /// <summary>The wall on a cell by name, or null when it is empty. Interior maps only.</summary>
    public string? WallAt(int x, int y) => Cell(TiledFormat.Interior, 1, x, y);

    /// <summary>The dressing on a cell by name, or null when it is empty. Interior maps only.</summary>
    public string? DressingAt(int x, int y) => Cell(TiledFormat.Interior, 2, x, y);

    private string? Cell(TiledFormat format, int layer, int x, int y)
    {
        if (Format != format)
        {
            throw new InvalidOperationException(
                $"Map '{MapId}' is an {Format.Name} map; it has no '{format.Layers[layer].Layer}' layer.");
        }
        return _grids[layer][x, y];
    }

    /// <summary>Throws <see cref="MapRecipeException"/> naming the file unless this map is
    /// <paramref name="format"/> — a map builds from its own format's file only.</summary>
    internal void RequireFormat(TiledFormat format)
    {
        if (Format != format)
        {
            throw new MapRecipeException(SourcePath,
                $"is an {Format.Name} map; map '{MapId}' builds from an {format.Name} file.");
        }
    }

    /// <summary>Whether a kind takes its tile span from the object's rectangle (a kerb
    /// cut's "w" is the columns it breaks; its "h" is always one row).</summary>
    private static bool IsSpanKind(string kind) =>
        kind is PlacementKinds.Exit or PlacementKinds.ShopCounter or PlacementKinds.KerbCut;

    /// <summary>The firstgid the WRITER gives layer <paramref name="layer"/>'s tileset:
    /// 1, then straight after the previous palette. The reader takes whatever a file declares.</summary>
    private int WriterFirstGid(int layer)
    {
        int firstGid = 1;
        for (int i = 0; i < layer; i++)
            firstGid += Format.Layers[i].Palette.Names.Count;
        return firstGid;
    }

    // ------------------------------------------------------------------
    // Writer
    // ------------------------------------------------------------------

    /// <summary>The canonical TMX text. Deterministic: same map, same bytes.</summary>
    public string ToTmx()
    {
        List<MapPlacement> ordered = Ordered(Placements);
        var text = new StringBuilder();
        text.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        text.Append("<map version=\"1.10\" tiledversion=\"1.12.0\" orientation=\"orthogonal\" renderorder=\"right-down\"")
            .Append(" width=\"").Append(MapRecipe.Number(Width)).Append('"')
            .Append(" height=\"").Append(MapRecipe.Number(Height)).Append('"')
            .Append(" tilewidth=\"").Append(MapRecipe.Number(Tile)).Append('"')
            .Append(" tileheight=\"").Append(MapRecipe.Number(Tile)).Append('"')
            .Append(" infinite=\"0\" nextlayerid=\"").Append(MapRecipe.Number(Format.Layers.Count + 2)).Append('"')
            .Append(" nextobjectid=\"").Append(MapRecipe.Number(ordered.Count + 1)).Append("\">\n");
        text.Append(" <properties>\n");
        text.Append("  <property name=\"").Append(MapIdProperty).Append("\" value=\"").Append(Escape(MapId)).Append("\"/>\n");
        text.Append(" </properties>\n");
        for (int i = 0; i < Format.Layers.Count; i++)
        {
            text.Append(" <tileset firstgid=\"").Append(MapRecipe.Number(WriterFirstGid(i)))
                .Append("\" source=\"").Append(Format.Layers[i].Palette.TilesetSource).Append("\"/>\n");
        }

        // Tile layer ids 1, 3, 4, ...: the placements group took 2 when there were
        // fewer layers, and the ids stay put.
        for (int i = 0; i < Format.Layers.Count; i++)
        {
            int layer = i;
            AppendLayer(text, i == 0 ? 1 : i + 2, Format.Layers[i].Layer, (x, y) => Gid(layer, x, y));
        }

        text.Append(" <objectgroup id=\"2\" name=\"").Append(PlacementLayer).Append("\">\n");
        for (int i = 0; i < ordered.Count; i++)
            AppendObject(text, ordered[i], i + 1);
        text.Append(" </objectgroup>\n");
        text.Append("</map>\n");
        return text.ToString();
    }

    private void AppendLayer(StringBuilder text, int id, string name, Func<int, int, int> gid)
    {
        text.Append(" <layer id=\"").Append(MapRecipe.Number(id)).Append("\" name=\"").Append(name).Append('"')
            .Append(" width=\"").Append(MapRecipe.Number(Width)).Append('"')
            .Append(" height=\"").Append(MapRecipe.Number(Height)).Append("\">\n");
        text.Append("  <data encoding=\"csv\">\n");
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (x > 0)
                    text.Append(',');
                text.Append(MapRecipe.Number(gid(x, y)));
            }
            text.Append(y == Height - 1 ? "\n" : ",\n");
        }
        text.Append("</data>\n");
        text.Append(" </layer>\n");
    }

    /// <summary>A cell's gid in the writer's numbering: 0 for an empty optional cell.</summary>
    private int Gid(int layer, int x, int y)
    {
        string? name = _grids[layer][x, y];
        if (name == null && layer > 0)
            return 0;
        TiledPalette palette = Format.Layers[layer].Palette;
        int index = IndexOf(palette.Names, name);
        if (index < 0)
        {
            string what = char.ToUpperInvariant(palette.Property[0]) + palette.Property[1..];
            throw new InvalidOperationException(
                $"{what} '{name}' at ({x},{y}) is not in the Tiled palette ({string.Join(", ", palette.Names)}).");
        }
        return WriterFirstGid(layer) + index;
    }

    private static void AppendObject(StringBuilder text, MapPlacement placement, int id)
    {
        if (placement.NudgeX != 0 || placement.NudgeY != 0)
        {
            throw new InvalidOperationException(
                $"Placement {placement} has a nudge; Tiled maps do not carry nudges.");
        }

        bool span = IsSpanKind(placement.Kind);
        int widthTiles = span ? placement.Int(PlacementFields.Width, 1) : 1;
        int heightTiles = span ? placement.Int(PlacementFields.Height, 1) : 1;

        var properties = new List<string>();
        foreach ((string key, string raw) in placement.Fields)
        {
            if (span && key is PlacementFields.Width or PlacementFields.Height)
                continue;
            properties.Add(PropertyLine(placement, key, raw));
        }

        text.Append("  <object id=\"").Append(MapRecipe.Number(id)).Append('"')
            .Append(" name=\"").Append(Escape(placement.Id)).Append('"')
            .Append(" type=\"").Append(Escape(placement.Kind)).Append('"')
            .Append(" x=\"").Append(MapRecipe.Number(placement.X * Tile)).Append('"')
            .Append(" y=\"").Append(MapRecipe.Number(placement.Y * Tile)).Append('"')
            .Append(" width=\"").Append(MapRecipe.Number(widthTiles * Tile)).Append('"')
            .Append(" height=\"").Append(MapRecipe.Number(heightTiles * Tile)).Append('"');
        if (properties.Count == 0)
        {
            text.Append("/>\n");
            return;
        }
        text.Append(">\n");
        text.Append("   <properties>\n");
        foreach (string line in properties)
            text.Append("    ").Append(line).Append('\n');
        text.Append("   </properties>\n");
        text.Append("  </object>\n");
    }

    private static string PropertyLine(MapPlacement placement, string key, string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        JsonElement value = document.RootElement;
        string name = Escape(key);
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return $"<property name=\"{name}\" value=\"{Escape(value.GetString()!)}\"/>";
            case JsonValueKind.Number when value.TryGetInt32(out int number):
                return $"<property name=\"{name}\" type=\"int\" value=\"{MapRecipe.Number(number)}\"/>";
            case JsonValueKind.True:
                return $"<property name=\"{name}\" type=\"bool\" value=\"true\"/>";
            case JsonValueKind.False:
                return $"<property name=\"{name}\" type=\"bool\" value=\"false\"/>";
            default:
                throw new InvalidOperationException(
                    $"Placement {placement} field '{key}' is {raw}; Tiled maps carry strings, whole numbers and bools.");
        }
    }

    /// <summary>XML-escapes attribute text: &amp; &lt; &gt; &quot; and newline (&amp;#10;).</summary>
    private static string Escape(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '&': text.Append("&amp;"); break;
                case '<': text.Append("&lt;"); break;
                case '>': text.Append("&gt;"); break;
                case '"': text.Append("&quot;"); break;
                case '\n': text.Append("&#10;"); break;
                default: text.Append(c); break;
            }
        }
        return text.ToString();
    }

    /// <summary>MapRecipe's canonical order: y, x, kind, id, then the whole line.</summary>
    private static List<MapPlacement> Ordered(MapRecipe recipe)
    {
        var ordered = new List<MapPlacement>(recipe.Placements);
        ordered.Sort(static (a, b) =>
        {
            int order = a.Y.CompareTo(b.Y);
            if (order != 0) return order;
            order = a.X.CompareTo(b.X);
            if (order != 0) return order;
            order = string.CompareOrdinal(a.Kind, b.Kind);
            if (order != 0) return order;
            order = string.CompareOrdinal(a.Id, b.Id);
            return order != 0 ? order : string.CompareOrdinal(MapRecipe.Line(a), MapRecipe.Line(b));
        });
        return ordered;
    }

    private static int IndexOf(IReadOnlyList<string> list, string? value)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == value)
                return i;
        return -1;
    }

    // ------------------------------------------------------------------
    // Reader
    // ------------------------------------------------------------------

    /// <summary>
    /// Reads TMX text. Throws <see cref="MapRecipeException"/> naming
    /// <paramref name="sourcePath"/> for anything outside the supported subset.
    /// </summary>
    public static TiledMap Parse(string tmx, string sourcePath)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(tmx);
        }
        catch (XmlException e)
        {
            throw new MapRecipeException(sourcePath, $"is not valid XML: {e.Message}");
        }

        XElement map = document.Root!;
        if (map.Name.LocalName != "map")
            throw new MapRecipeException(sourcePath, "must hold a <map> at its root.");

        if (Attr(map, "orientation") != "orthogonal")
            throw new MapRecipeException(sourcePath, $"is '{Attr(map, "orientation")}'; only orthogonal maps are read.");
        if (Attr(map, "tilewidth") != "16" || Attr(map, "tileheight") != "16")
            throw new MapRecipeException(sourcePath, "must use 16x16 tiles.");
        if ((Attr(map, "infinite") ?? "0") != "0")
            throw new MapRecipeException(sourcePath, "is an infinite map; untick Infinite in Map Properties.");

        int width = RequireInt(map, "width", sourcePath);
        int height = RequireInt(map, "height", sourcePath);

        string? mapId = null;
        foreach (XElement property in Children(map, "properties").SelectMany(p => Children(p, "property")))
        {
            if (Attr(property, "name") == MapIdProperty)
                mapId = Attr(property, "value") ?? property.Value;
        }
        if (mapId is not { Length: > 0 })
            throw new MapRecipeException(sourcePath, $"has no '{MapIdProperty}' map property naming the map it builds.");

        if (Children(map, "group").Any() || Children(map, "imagelayer").Any())
            throw new MapRecipeException(sourcePath, "has a group or image layer; only tile layers and one object group are read.");

        // Tilesets: external, each one palette's (Name).tsx at most once, all of ONE
        // format, that format's first palette required.
        string tilesetRule = "it must reference one format's palettes, the first required: " + string.Join("; or ",
            TiledFormat.All.Select(format => string.Join(" and, optionally, ",
                format.Layers.Select(layer => layer.Palette.TilesetSource))));
        List<XElement> tilesets = Children(map, "tileset").ToList();
        if (tilesets.Count == 0)
            throw new MapRecipeException(sourcePath, $"has no tilesets; {tilesetRule}.");
        var declared = new List<(TiledPalette Palette, int FirstGid)>();
        foreach (XElement tileset in tilesets)
        {
            string? tilesetSource = Attr(tileset, "source");
            if (tilesetSource == null)
                throw new MapRecipeException(sourcePath, $"embeds a tileset; {tilesetRule}.");
            string fileName = tilesetSource[(tilesetSource.LastIndexOf('/') + 1)..];
            int firstGid = RequireInt(tileset, "firstgid", sourcePath);
            TiledPalette? palette = TiledPalette.All.FirstOrDefault(p => $"{p.Name}.tsx" == fileName);
            if (palette == null)
                throw new MapRecipeException(sourcePath, $"uses tileset '{tilesetSource}'; {tilesetRule}.");
            if (declared.Any(d => d.Palette == palette))
                throw new MapRecipeException(sourcePath, $"references {fileName} twice; {tilesetRule}.");
            declared.Add((palette, firstGid));
        }
        List<TiledFormat> formats = declared
            .Select(d => TiledFormat.All.First(format => format.Layers.Any(layer => layer.Palette == d.Palette)))
            .Distinct()
            .ToList();
        if (formats.Count > 1)
        {
            throw new MapRecipeException(sourcePath,
                $"mixes {string.Join(" and ", formats.Select(f => f.Name))} tilesets; {tilesetRule}.");
        }
        TiledFormat mapFormat = formats[0];
        if (declared.All(d => d.Palette != mapFormat.Layers[0].Palette))
        {
            throw new MapRecipeException(sourcePath,
                $"has no {mapFormat.Layers[0].Palette.TilesetSource} tileset; {tilesetRule}.");
        }

        // A gid belongs to the tileset with the greatest firstgid at or below it.
        (TiledPalette Palette, long Local)? Resolve(long gid)
        {
            (TiledPalette, long)? best = null;
            int bestFirst = int.MinValue;
            foreach ((TiledPalette palette, int first) in declared)
            {
                if (first <= gid && first > bestFirst)
                {
                    best = (palette, gid - first);
                    bestFirst = first;
                }
            }
            return best;
        }

        // Tile layers: the format's names, each at most once, any order, the first required.
        IReadOnlyList<(string Layer, TiledPalette Palette)> formatLayers = mapFormat.Layers;
        string layerNames = string.Join(", ", formatLayers.Select(layer => $"'{layer.Layer}'"));
        List<XElement> layers = Children(map, "layer").ToList();
        if (layers.Count < 1 || layers.Count > formatLayers.Count)
        {
            throw new MapRecipeException(sourcePath,
                $"has {layers.Count} tile layers; an {mapFormat.Name} map must have '{formatLayers[0].Layer}' and, optionally, the rest of {layerNames}.");
        }
        var layerElements = new XElement?[formatLayers.Count];
        foreach (XElement layer in layers)
        {
            string? name = Attr(layer, "name");
            int index = -1;
            for (int i = 0; i < formatLayers.Count; i++)
                if (formatLayers[i].Layer == name)
                    index = i;
            if (index < 0)
                throw new MapRecipeException(sourcePath, $"names a tile layer '{name}'; an {mapFormat.Name} map's tile layers are {layerNames}.");
            if (layerElements[index] != null)
                throw new MapRecipeException(sourcePath, $"has two '{name}' tile layers; it must have one of each.");
            layerElements[index] = layer;
        }
        if (layerElements[0] == null)
            throw new MapRecipeException(sourcePath, $"has no '{formatLayers[0].Layer}' tile layer.");

        var grids = new string?[formatLayers.Count][,];
        for (int layer = 0; layer < formatLayers.Count; layer++)
        {
            grids[layer] = new string?[width, height];
            if (layerElements[layer] is not { } element)
                continue;
            (string layerName, TiledPalette palette) = formatLayers[layer];
            long[] gids = ReadCsv(element, layerName, width, height, sourcePath);
            for (int i = 0; i < gids.Length; i++)
            {
                int x = i % width, y = i / width;
                long gid = gids[i];
                if (gid == 0 && layer > 0)
                    continue;
                if (gid == 0 || Resolve(gid) is not { } hit || hit.Palette != palette || hit.Local >= palette.Names.Count)
                {
                    throw new MapRecipeException(sourcePath, layer == 0
                        ? $"has no {palette.Property} at ({x},{y}) (tile {gid}); paint every cell from the {palette.Name} palette."
                        : $"has tile {gid} at ({x},{y}) in '{layerName}', which is not in the {palette.Name} palette; paint the {layerName} layer from the {palette.Name} palette.");
                }
                grids[layer][x, y] = palette.Names[(int)hit.Local];
            }
        }

        // Placements.
        List<XElement> groups = Children(map, "objectgroup").ToList();
        if (groups.Count != 1)
            throw new MapRecipeException(sourcePath, $"has {groups.Count} object layers; it must have exactly one, '{PlacementLayer}'.");
        if (Attr(groups[0], "name") != PlacementLayer)
            throw new MapRecipeException(sourcePath, $"names its object layer '{Attr(groups[0], "name")}'; it must be '{PlacementLayer}'.");

        var recipe = new MapRecipe(mapId);
        foreach (XElement element in Children(groups[0], "object"))
            recipe.Add(ParseObject(element, sourcePath));

        return new TiledMap(mapFormat, mapId, grids, recipe, sourcePath);
    }

    /// <summary>A map-sized CSV tile layer's gids, flip bits masked, row-major.</summary>
    private static long[] ReadCsv(XElement layer, string name, int width, int height, string sourcePath)
    {
        if (RequireInt(layer, "width", sourcePath) != width || RequireInt(layer, "height", sourcePath) != height)
            throw new MapRecipeException(sourcePath, $"has a '{name}' layer that is not the map's {width}x{height}.");
        XElement? data = Children(layer, "data").FirstOrDefault();
        if (data == null || Attr(data, "encoding") != "csv" || Attr(data, "compression") != null)
            throw new MapRecipeException(sourcePath, "is not CSV; set Tile Layer Format to CSV in Map Properties.");

        string[] tokens = data.Value.Split(',', StringSplitOptions.TrimEntries);
        if (tokens.Length != width * height)
            throw new MapRecipeException(sourcePath, $"has {tokens.Length} cells in '{name}'; a {width}x{height} map needs {width * height}.");

        var gids = new long[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!uint.TryParse(tokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out uint raw))
                throw new MapRecipeException(sourcePath, $"has '{tokens[i]}' at ({i % width},{i / width}) in '{name}'; expected a tile id.");
            gids[i] = raw & FlipMask;
        }
        return gids;
    }

    private static MapPlacement ParseObject(XElement element, string sourcePath)
    {
        string label = $"object {Attr(element, "id") ?? "?"}";
        if (Attr(element, "gid") != null || Attr(element, "template") != null)
            throw new MapRecipeException(sourcePath, $"{label} is a tile or template object; placements are plain rectangles.");
        foreach (string shape in new[] { "point", "ellipse", "polygon", "polyline", "text" })
        {
            if (Children(element, shape).Any())
                throw new MapRecipeException(sourcePath, $"{label} is a {shape}; placements are plain rectangles.");
        }

        string? kind = Attr(element, "type") ?? Attr(element, "class");
        if (kind is not { Length: > 0 })
            throw new MapRecipeException(sourcePath, $"{label} has no class (the placement kind).");
        string? id = Attr(element, "name");
        if (id is not { Length: > 0 })
            throw new MapRecipeException(sourcePath, $"{label} has no name (the placement id).");

        int x = Cells(element, "x", label, sourcePath, required: true);
        int y = Cells(element, "y", label, sourcePath, required: true);
        var placement = new MapPlacement(kind, id, x, y);

        foreach (XElement property in Children(element, "properties").SelectMany(p => Children(p, "property")))
        {
            string name = Attr(property, "name") ?? "";
            if (name.Length == 0)
                throw new MapRecipeException(sourcePath, $"{label} has a property with no name.");
            if (MapPlacement.ReservedKeys.Contains(name))
                throw new MapRecipeException(sourcePath, $"{label} property '{name}' shadows one of the placement's own fields.");
            string type = Attr(property, "type") ?? "string";
            string value = Attr(property, "value") ?? property.Value;
            switch (type)
            {
                case "string":
                    placement.SetText(name, value);
                    break;
                case "int":
                    if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
                        throw new MapRecipeException(sourcePath, $"{label} property '{name}' is not a whole number.");
                    placement.SetInt(name, number);
                    break;
                case "bool":
                    if (value is not ("true" or "false"))
                        throw new MapRecipeException(sourcePath, $"{label} property '{name}' is not true or false.");
                    placement.SetBool(name, value == "true");
                    break;
                default:
                    throw new MapRecipeException(sourcePath,
                        $"{label} property '{name}' is a {type}; placements carry string, int and bool properties only.");
            }
        }

        if (IsSpanKind(kind))
        {
            placement.SetInt(PlacementFields.Width, Cells(element, "width", label, sourcePath, required: false));
            placement.SetInt(PlacementFields.Height, Cells(element, "height", label, sourcePath, required: false));
        }
        return placement;
    }

    /// <summary>A pixel attribute as whole tiles; off-grid throws. An absent optional size is one tile.</summary>
    private static int Cells(XElement element, string attribute, string label, string sourcePath, bool required)
    {
        string? text = Attr(element, attribute);
        if (text == null)
        {
            if (required)
                throw new MapRecipeException(sourcePath, $"{label} has no '{attribute}'.");
            return 1;
        }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double pixels)
            || pixels % Tile != 0)
        {
            throw new MapRecipeException(sourcePath,
                $"{label} {attribute}={text} is off the 16px grid; enable Snap to Grid and move it onto a cell.");
        }
        return (int)(pixels / Tile);
    }

    private static int RequireInt(XElement element, string attribute, string sourcePath) =>
        int.TryParse(Attr(element, attribute), NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new MapRecipeException(sourcePath, $"has a <{element.Name.LocalName}> with no whole-number '{attribute}'.");

    private static string? Attr(XElement element, string name) => element.Attribute(name)?.Value;

    private static IEnumerable<XElement> Children(XElement element, string name) =>
        element.Elements().Where(child => child.Name.LocalName == name);
}
