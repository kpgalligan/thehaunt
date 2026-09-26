using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace TheHaunt.World;

/// <summary>
/// One map as a Tiled TMX file: a surface grid BY NAME (what each cell IS — the tile a
/// person paints with in Tiled is only a swatch from <see cref="TiledSurfaces"/>), an
/// optional obstacle grid BY NAME (what stands on a cell — swatches from
/// <see cref="TiledObstacles"/>; null = empty), and the map's placements as rectangle
/// objects. Every exterior's build (<see cref="ExteriorMap"/>) reads all three; tile
/// painting stays generative. Span kinds (exit, shop_counter, kerb_cut) carry their
/// "w"/"h" as the rectangle's size, not as properties.
///
/// The reader is a STRICT subset of TMX — the shape the writer emits and Tiled saves back:
/// orthogonal, 16px, finite; one or two external tilesets (surfaces.tsx, required, and
/// obstacles.tsx, optional — each at most once); one or two map-sized CSV tile layers
/// ("surface", required, and "obstacles", optional — each at most once, in any order);
/// one object group named "placements" of plain rectangles on the grid. A gid resolves to
/// the tileset with the greatest firstgid at or below it, so a file saved before a
/// surface was appended still reads. No obstacles layer means every cell is empty, and
/// the layer needs no obstacles.tsx while all its cells are 0. Anything else throws
/// <see cref="MapRecipeException"/> naming the file, because a map half-read is a map
/// quietly missing things. <c>&lt;editorsettings&gt;</c> and unknown attributes are
/// ignored.
///
/// The writer is canonical and deterministic ("\n" endings, objects in MapRecipe's
/// (y, x, kind, id) order, ids 1..N, property keys ordinal-sorted), laid out the way
/// Tiled itself lays a TMX out, so a Tiled save of an untouched map is a near no-op diff.
/// </summary>
public sealed class TiledMap
{
    public const string SurfaceLayer = "surface";
    public const string PlacementLayer = "placements";
    public const string ObstacleLayer = "obstacles";
    public const string MapIdProperty = "map";

    private const int Tile = MapRoot.TileSize;
    private const uint FlipMask = 0x0FFFFFFF;
    private const string TilesetFileName = "surfaces.tsx";
    private const string ObstacleTilesetFileName = "obstacles.tsx";

    private readonly string[,] _surfaces;
    private readonly string?[,] _obstacles;

    /// <summary>A map with no obstacles.</summary>
    /// <param name="surfaces">Surface names, indexed [x, y].</param>
    public TiledMap(string mapId, string[,] surfaces, MapRecipe placements, string sourcePath)
        : this(mapId, surfaces, new string?[surfaces.GetLength(0), surfaces.GetLength(1)], placements, sourcePath)
    {
    }

    /// <param name="surfaces">Surface names, indexed [x, y].</param>
    /// <param name="obstacles">Obstacle names, indexed [x, y], null = empty; the surfaces' size.</param>
    public TiledMap(string mapId, string[,] surfaces, string?[,] obstacles, MapRecipe placements, string sourcePath)
    {
        if (obstacles.GetLength(0) != surfaces.GetLength(0) || obstacles.GetLength(1) != surfaces.GetLength(1))
        {
            throw new ArgumentException(
                $"Obstacles are {obstacles.GetLength(0)}x{obstacles.GetLength(1)}, but surfaces are " +
                $"{surfaces.GetLength(0)}x{surfaces.GetLength(1)}.", nameof(obstacles));
        }
        MapId = mapId;
        _surfaces = surfaces;
        _obstacles = obstacles;
        Placements = placements;
        SourcePath = sourcePath;
    }

    public string MapId { get; }
    public int Width => _surfaces.GetLength(0);
    public int Height => _surfaces.GetLength(1);

    /// <summary>The file (or label) this map came from — named in every error about it.</summary>
    public string SourcePath { get; }

    public MapRecipe Placements { get; }

    public string SurfaceAt(int x, int y) => _surfaces[x, y];

    /// <summary>The obstacle on a cell by name, or null when it is empty.</summary>
    public string? ObstacleAt(int x, int y) => _obstacles[x, y];

    /// <summary>Whether a kind takes its tile span from the object's rectangle (a kerb
    /// cut's "w" is the columns it breaks; its "h" is always one row).</summary>
    private static bool IsSpanKind(string kind) =>
        kind is PlacementKinds.Exit or PlacementKinds.ShopCounter or PlacementKinds.KerbCut;

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
            .Append(" infinite=\"0\" nextlayerid=\"4\"")
            .Append(" nextobjectid=\"").Append(MapRecipe.Number(ordered.Count + 1)).Append("\">\n");
        text.Append(" <properties>\n");
        text.Append("  <property name=\"").Append(MapIdProperty).Append("\" value=\"").Append(Escape(MapId)).Append("\"/>\n");
        text.Append(" </properties>\n");
        text.Append(" <tileset firstgid=\"").Append(MapRecipe.Number(TiledSurfaces.FirstGid))
            .Append("\" source=\"").Append(TiledSurfaces.TilesetSource).Append("\"/>\n");
        text.Append(" <tileset firstgid=\"").Append(MapRecipe.Number(TiledObstacles.FirstGid))
            .Append("\" source=\"").Append(TiledObstacles.TilesetSource).Append("\"/>\n");

        AppendLayer(text, 1, SurfaceLayer, (x, y) => Gid(_surfaces[x, y], x, y));
        AppendLayer(text, 3, ObstacleLayer, (x, y) => ObstacleGid(_obstacles[x, y], x, y));

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

    private static int ObstacleGid(string? obstacle, int x, int y)
    {
        if (obstacle == null)
            return 0;
        int index = IndexOf(TiledObstacles.Names, obstacle);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Obstacle '{obstacle}' at ({x},{y}) is not in the Tiled palette ({string.Join(", ", TiledObstacles.Names)}).");
        }
        return TiledObstacles.FirstGid + index;
    }

    private static int Gid(string surface, int x, int y)
    {
        int index = IndexOf(TiledSurfaces.Names, surface);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Surface '{surface}' at ({x},{y}) is not in the Tiled palette ({string.Join(", ", TiledSurfaces.Names)}).");
        }
        return TiledSurfaces.FirstGid + index;
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

    private static int IndexOf(IReadOnlyList<string> list, string value)
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
            throw new MapRecipeException(sourcePath, "has a group or image layer; only one tile layer and one object group are read.");

        // Tilesets: surfaces.tsx (required) and obstacles.tsx (optional), external, each once.
        const string tilesetRule = $"it must reference {TiledSurfaces.TilesetSource} and, optionally, {TiledObstacles.TilesetSource}";
        List<XElement> tilesets = Children(map, "tileset").ToList();
        if (tilesets.Count is < 1 or > 2)
            throw new MapRecipeException(sourcePath, $"has {tilesets.Count} tilesets; {tilesetRule}.");
        int? surfaceFirstGid = null, obstacleFirstGid = null;
        foreach (XElement tileset in tilesets)
        {
            string? tilesetSource = Attr(tileset, "source");
            if (tilesetSource == null)
                throw new MapRecipeException(sourcePath, $"embeds a tileset; {tilesetRule}.");
            string fileName = tilesetSource[(tilesetSource.LastIndexOf('/') + 1)..];
            int firstGid = RequireInt(tileset, "firstgid", sourcePath);
            if (fileName == TilesetFileName && surfaceFirstGid == null)
                surfaceFirstGid = firstGid;
            else if (fileName == ObstacleTilesetFileName && obstacleFirstGid == null)
                obstacleFirstGid = firstGid;
            else if (fileName is TilesetFileName or ObstacleTilesetFileName)
                throw new MapRecipeException(sourcePath, $"references {fileName} twice; {tilesetRule}.");
            else
                throw new MapRecipeException(sourcePath, $"uses tileset '{tilesetSource}'; {tilesetRule}.");
        }
        if (surfaceFirstGid == null)
            throw new MapRecipeException(sourcePath, $"has no {TiledSurfaces.TilesetSource} tileset; {tilesetRule}.");

        // A gid belongs to the tileset with the greatest firstgid at or below it.
        (bool Obstacle, long Local)? Resolve(long gid)
        {
            (bool, long)? best = null;
            int bestFirst = int.MinValue;
            if (surfaceFirstGid.Value <= gid && surfaceFirstGid.Value > bestFirst)
            {
                best = (false, gid - surfaceFirstGid.Value);
                bestFirst = surfaceFirstGid.Value;
            }
            if (obstacleFirstGid is int first && first <= gid && first > bestFirst)
                best = (true, gid - first);
            return best;
        }

        // Tile layers: "surface" (required) and "obstacles" (optional), each once, any order.
        List<XElement> layers = Children(map, "layer").ToList();
        if (layers.Count is < 1 or > 2)
            throw new MapRecipeException(sourcePath,
                $"has {layers.Count} tile layers; it must have '{SurfaceLayer}' and, optionally, '{ObstacleLayer}'.");
        XElement? surfaceLayer = null, obstacleLayer = null;
        foreach (XElement layer in layers)
        {
            string? name = Attr(layer, "name");
            if (name == SurfaceLayer && surfaceLayer == null)
                surfaceLayer = layer;
            else if (name == ObstacleLayer && obstacleLayer == null)
                obstacleLayer = layer;
            else if (name is SurfaceLayer or ObstacleLayer)
                throw new MapRecipeException(sourcePath, $"has two '{name}' tile layers; it must have one of each.");
            else
                throw new MapRecipeException(sourcePath,
                    $"names a tile layer '{name}'; tile layers must be '{SurfaceLayer}' or '{ObstacleLayer}'.");
        }
        if (surfaceLayer == null)
            throw new MapRecipeException(sourcePath, $"has no '{SurfaceLayer}' tile layer.");

        long[] surfaceGids = ReadCsv(surfaceLayer, SurfaceLayer, width, height, sourcePath);
        var surfaces = new string[width, height];
        for (int i = 0; i < surfaceGids.Length; i++)
        {
            int x = i % width, y = i / width;
            long gid = surfaceGids[i];
            if (gid == 0 || Resolve(gid) is not { Obstacle: false } hit || hit.Local >= TiledSurfaces.Names.Count)
                throw new MapRecipeException(sourcePath, $"has no surface at ({x},{y}) (tile {gid}); paint every cell from the surfaces palette.");
            surfaces[x, y] = TiledSurfaces.Names[(int)hit.Local];
        }

        var obstacles = new string?[width, height];
        if (obstacleLayer != null)
        {
            long[] obstacleGids = ReadCsv(obstacleLayer, ObstacleLayer, width, height, sourcePath);
            for (int i = 0; i < obstacleGids.Length; i++)
            {
                int x = i % width, y = i / width;
                long gid = obstacleGids[i];
                if (gid == 0)
                    continue;
                if (Resolve(gid) is not { Obstacle: true } hit || hit.Local >= TiledObstacles.Names.Count)
                    throw new MapRecipeException(sourcePath,
                        $"has tile {gid} at ({x},{y}) in '{ObstacleLayer}', which is not an obstacle; paint the obstacles layer from the obstacles palette.");
                obstacles[x, y] = TiledObstacles.Names[(int)hit.Local];
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

        return new TiledMap(mapId, surfaces, obstacles, recipe, sourcePath);
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
