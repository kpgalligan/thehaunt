using System.Globalization;
using System.Text;
using Godot;
using TheHaunt.Content;
using TheHaunt.Core;
using Entry = TheHaunt.World.WorldDumpEntry;

namespace TheHaunt.World;

/// <summary>
/// Exports every EXTERIOR map's geography as one JSON document — the input the intro
/// flyover's Blender generator (tools/flyover) builds the 3D town from, so no
/// coordinate is ever transcribed by hand. Written by Main's <c>--dump-world</c> dev
/// flag; the output is GENERATED: never commit a dump.
///
/// Read from the maps themselves: each exterior (MapIds.All minus interiors, in that
/// order) is built headless under a host node, exactly as the game builds it, and
/// read back — its <see cref="ISurfaceGrid"/>, its Obstacles layer, and its nodes
/// (facades, placeholders, props, signs, lights, doors, exits, spawn markers). The
/// few solved-in-code shapes no node carries (the farm's pen, storm debris and
/// recipe scatter, the motor court's parts, the plaza's worn stone, kerb cuts) come
/// through small read-only views on their map classes. Canonical state, not a save:
/// non-farm maps get <c>ApplyState(new MapState())</c> like a first visit; the farm's
/// per-save field obstacles are replaced by a fixed-seed sample (<see cref="SampleSeed"/>)
/// marked <c>"sample": true</c>, and flag/clock-gated things are exported present with a
/// <c>"conditional"</c> note. Deterministic: two renders are byte-identical.
///
/// Schema (version 1). Coordinates are tiles (16px), x east, y south, row 0 north;
/// every rect is top-left x,y plus w,h. <c>px</c> pairs are map pixels.
/// <code>
/// { "version": 1, "tilePx": 16, "sampleSeed": 1,
///   "maps": [ {
///     "id", "width", "height",
///     "legend":   { char: surface name }            // only the kinds this map uses
///     "surfaces": [ string x height ]                // width chars each
///     "blocked":  [ string x height ]                // '#' Obstacles-layer cell, 'D' door, '.' open
///                                                    //   (woods, water and farm-sheet solids block via
///                                                    //   their ground tiles, not this layer; the town's
///                                                    //   fences and bushes are on it, so show as '#')
///     "buildings": [ { "id", "place" (interior map id its door leads to, or null),
///         "art" (town_hall|general_store|motel|farmhouse|barn, or null = placeholder),
///         "x","y","w","h" (blocked FOOTPRINT), "drawnW","drawnH" (drawn size, tiles),
///         "wall" ("#rrggbb" placeholder face, or null), "door" ({x,y} or null),
///         "label" (its wall-band/bracket lettering, or null), "state" (barn state,
///         "boarded", or null), "texture" + "source" [x,y,w,h] (art only);
///         motel adds "anchorPx", "extentPx" [left,top,right,bottom], "parts" (office,
///         strip), "rooms" (door, doorColor, to, lockedBy), "lettering" } ],
///     "props":   [ { "kind", "id" (node/placement name or null), "x","y","w","h" (footprint),
///         kind extras: well|bench|notice_board|planter ("variant"), chain ("board": sign id),
///         pit_cover, screen/speaker ("drawnH"), car ("paint", "facing" N|W = the nose,
///         "footprintPx" [x,y,w,h] map px: the ground it stands on), mailbox,
///         shipping_bin, scatter (recipe id), fence ("gate"), debris ("piece"),
///         tree|stump|rock ("sample", tree "variant"), worn_cobble, kerb_cut ("side": N|S),
///         stall_stripes (rect = the lot; "stripesPx" [[x,y,w,h]] lot-local px),
///         ramp_rows (rect = the field; "rowsPx" [y] field-local px),
///         "conditional" when gated } ],
///     "lights":  [ { "kind": street|glow, "x","y", "px", street: "facing" (W|E, the arm),
///         "lit"; glow: "falloff", "color", "parent" } ],
///     "signs":   [ { "kind": board|pole|wallband|bracket|neon|for_sale|read, "id", "x","y",
///         "px", "text" (drawn lettering, or null), "readText" (what reading it shows, or
///         null), "building" (mounted on), extras: pole "lines"/"face"/"letters", motel
///         "neon"/"neonDark"/"blinks"/"nameplateBlank", wallband "lit", neon "onMinutes"
///         [[first,last] minute-of-day, 0 = 6:00], for_sale "soldLine", "conditional" } ],
///     "doors":   [ { "x","y", "to", "spawn", "lockedBy" (flag or null), "building" } ],
///     "exits":   [ { "id", "edge" (N|S|E|W), "x","y","w","h", "to", "spawn", "spawnX",
///         "spawnY" (the destination marker's tile), "wrap", "conditional" } ],
///     "spawns":  [ { "name", "x", "y" } ] } ] }
/// </code>
/// Lists are sorted by y, x, kind, id; one entry per line.
/// </summary>
public static class WorldDump
{
    public const int Version = 1;

    /// <summary>The fixed ObstacleGen seed for the farm's representative trees, stumps
    /// and rocks (the real ones are random per save).</summary>
    public const int SampleSeed = 1;

    private const string WrapNote =
        "road wrap (RoadWrap): leaving town past this edge arrives at the other entry";
    private const string DebrisNote = "present until " + StoryKeys.RoadCleared + " (the storm blockade)";
    private const string TheaterChainNote =
        "up outside the drive-in's summer trading hours (DriveIn.ChainDown)";

    /// <summary>Gated nodes, by (map, node name). Every gated exit must be listed, and
    /// every entry must match a node — a rename fails the dump instead of silently
    /// dropping the note.</summary>
    private static readonly Dictionary<(string MapId, string Node), string> Conditional = new()
    {
        [(MapIds.Farm, "BlockadeSign")] = DebrisNote,
        [(MapIds.Farm, "Exit_" + MapIds.Fork)] = "open once " + StoryKeys.RoadCleared + " is stamped",
        [(MapIds.EastFork, "TheaterChain")] = TheaterChainNote,
        [(MapIds.EastFork, "TheaterChainSign")] = TheaterChainNote,
        [(MapIds.EastFork, "Exit_" + MapIds.DriveIn)] = "open only while the drive-in chain is down (DriveIn.ChainDown)",
    };

    /// <summary>One legend character per surface kind, the same in every map.</summary>
    private static readonly Dictionary<string, char> SurfaceCodes = new()
    {
        ["Grass"] = 'G', ["Dirt"] = 'D', ["Gravel"] = 'V', ["Cobble"] = 'C', ["Woods"] = 'W',
        ["Asphalt"] = 'A', ["Concrete"] = 'K', ["Road"] = 'R', ["Pasture"] = 'P', ["Path"] = 'T',
        ["Water"] = 'L', ["DeepWater"] = 'M',
    };

    /// <summary>The exported maps: every registered exterior, in MapIds.All order.</summary>
    public static IReadOnlyList<string> ExteriorIds() =>
        MapIds.All.Where(id => !MapIds.IsInterior(id) && MapRegistry.Contains(id)).ToList();

    /// <summary>Builds every exterior under <paramref name="host"/> (which must be in the
    /// tree), reads it, frees it, and returns the JSON document ("\n" line endings).</summary>
    public static string Render(Node host)
    {
        var built = new List<MapRoot>();
        try
        {
            foreach (string id in ExteriorIds())
            {
                MapRoot map = MapRegistry.Create(id);
                host.AddChild(map);
                // The farm's ApplyState paints SAVE state (soil, crops, per-save
                // obstacles, flag toggles); the dump shows the canonical fresh view.
                if (map is not TestMap)
                    map.ApplyState(new MapState());
                built.Add(map);
            }

            var spawns = built.ToDictionary(m => m.MapId, SpawnsOf);
            var used = new HashSet<(string, string)>();
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(Version).Append(",\n");
            sb.Append("  \"tilePx\": ").Append(MapRoot.TileSize).Append(",\n");
            sb.Append("  \"sampleSeed\": ").Append(SampleSeed).Append(",\n");
            sb.Append("  \"maps\": [\n");
            for (int i = 0; i < built.Count; i++)
            {
                RenderMap(sb, built[i], spawns, used);
                sb.Append(i < built.Count - 1 ? "    },\n" : "    }\n");
            }
            sb.Append("  ]\n}\n");

            foreach ((string MapId, string Node) key in Conditional.Keys)
            {
                if (!used.Contains(key))
                    throw new InvalidOperationException(
                        $"WorldDump: conditional note for '{key.Node}' on '{key.MapId}' matched no node.");
            }
            return sb.ToString();
        }
        finally
        {
            foreach (MapRoot map in built)
                map.Free();
        }
    }

    // ------------------------------------------------------------------
    // One map
    // ------------------------------------------------------------------

    private static void RenderMap(StringBuilder sb, MapRoot map,
        IReadOnlyDictionary<string, Dictionary<string, Vector2I>> spawns, HashSet<(string, string)> used)
    {
        var grid = map as ISurfaceGrid
            ?? throw new InvalidOperationException($"WorldDump: exterior '{map.MapId}' has no surface grid.");
        int width = grid.GridWidth, height = grid.GridHeight;
        TileMapLayer? obstacles = map.GetNodeOrNull<TileMapLayer>("Obstacles");

        var nodes = new List<Node>();
        Collect(map, nodes);
        List<Door> doors = nodes.OfType<Door>().ToList();
        var doorTiles = doors.Select(d => CellOf(map, d)).ToHashSet();

        bool Blocked(int x, int y) =>
            doorTiles.Contains(new Vector2I(x, y))
            || (obstacles != null && obstacles.GetCellSourceId(new Vector2I(x, y)) != -1);

        string? Note(Node node)
        {
            var key = (map.MapId, node.Name.ToString());
            if (!Conditional.TryGetValue(key, out string? note))
                return null;
            used.Add(key);
            return note;
        }

        // Surfaces, legend, blocked.
        var legend = new SortedDictionary<char, string>();
        var surfaceRows = new List<string>();
        var blockedRows = new List<string>();
        for (int y = 0; y < height; y++)
        {
            var row = new StringBuilder(width);
            var blockedRow = new StringBuilder(width);
            for (int x = 0; x < width; x++)
            {
                string name = grid.SurfaceName(x, y);
                if (!SurfaceCodes.TryGetValue(name, out char code))
                    throw new InvalidOperationException($"WorldDump: no legend code for surface '{name}'.");
                legend[code] = name;
                row.Append(code);
                blockedRow.Append(doorTiles.Contains(new Vector2I(x, y)) ? 'D' : Blocked(x, y) ? '#' : '.');
            }
            surfaceRows.Add(row.ToString());
            blockedRows.Add(blockedRow.ToString());
        }

        // Buildings first: prop footprints stop at a building's.
        var buildings = new List<Entry>();
        var footprints = new List<Rect2I>();
        Door? DoorIn(Rect2I rect) =>
            doors.Where(d => rect.HasPoint(CellOf(map, d)))
                .OrderBy(d => CellOf(map, d).Y).ThenBy(d => CellOf(map, d).X).FirstOrDefault();

        foreach (Node node in nodes)
        {
            switch (node)
            {
                case PlaceholderBuilding pb:
                {
                    Rect2I rect = AnchoredRect(map, pb, pb.TilesWide, pb.FootprintRows);
                    footprints.Add(rect);
                    Door? door = DoorIn(rect);
                    string? label = pb.GetChildren().OfType<WallBandSign>().Select(s => s.Text)
                        .Concat(pb.GetChildren().OfType<BracketSign>().Select(s => s.Text)).FirstOrDefault();
                    buildings.Add(new Entry(rect.Position.Y, rect.Position.X, "building", pb.Name, Fields(
                        ("id", pb.Name.ToString()), ("place", door?.TargetMapId), ("art", null),
                        ("x", rect.Position.X), ("y", rect.Position.Y), ("w", rect.Size.X), ("h", rect.Size.Y),
                        ("drawnW", pb.TilesWide), ("drawnH", pb.FootprintRows + PlaceholderBuilding.RoofRows),
                        ("wall", pb.Wall), ("door", DoorJson(map, door)), ("label", label),
                        ("state", pb.Boarded ? "boarded" : null))));
                    break;
                }
                case MotelFacade motel:
                    buildings.Add(MotelEntry(map, motel, doors, footprints));
                    break;
                case Prop prop when FacadeArt(prop) is { } art:
                {
                    int drawnW = Mathf.RoundToInt(prop.Source.Size.X / MapRoot.TileSize);
                    int drawnH = Mathf.RoundToInt(prop.Source.Size.Y / MapRoot.TileSize);
                    Vector2 p = Local(map, prop);
                    int x0 = Mathf.RoundToInt((p.X - drawnW * MapRoot.TileSize / 2f) / MapRoot.TileSize);
                    int bottom = BaseRow(p.Y);
                    int rows = Math.Max(1, ScanRows(Blocked, x0, drawnW, bottom, drawnH, footprints));
                    var rect = new Rect2I(x0, bottom - rows + 1, drawnW, rows);
                    footprints.Add(rect);
                    Door? door = DoorIn(rect);
                    string? state = null;
                    Rect2 source = prop.Source;
                    if (prop is BarnFacade)
                    {
                        int barn = BarnRules.StateOf(GameData.NewGame());
                        state = barn == BarnRules.Restored ? "restored"
                            : barn == BarnRules.Weathertight ? "weathertight" : "derelict";
                        source = BarnFacade.Variant(barn);
                    }
                    buildings.Add(new Entry(rect.Position.Y, rect.Position.X, "building", prop.Name, Fields(
                        ("id", prop.Name.ToString()), ("place", door?.TargetMapId), ("art", art),
                        ("x", rect.Position.X), ("y", rect.Position.Y), ("w", rect.Size.X), ("h", rect.Size.Y),
                        ("drawnW", drawnW), ("drawnH", drawnH), ("wall", null),
                        ("door", DoorJson(map, door)), ("label", null), ("state", state),
                        ("texture", prop.TexturePath), ("source", RectJson(source)))));
                    break;
                }
            }
        }

        string? BuildingAt(Vector2I cell)
        {
            foreach (Entry b in buildings)
            {
                if (b.Rect is { } r && r.HasPoint(cell))
                    return b.Id;
            }
            return null;
        }

        // Props, lights, signs.
        var props = new List<Entry>();
        var lights = new List<Entry>();
        var signs = new List<Entry>();
        var chains = new List<(Entry Entry, Rect2I Rect)>();
        var readAreas = new List<Sign>();

        Entry PropEntry(Node node, string kind, Rect2I rect, params (string, object?)[] extra)
        {
            string? id = NameOf(node);
            var fields = new List<(string, object?)>
            {
                ("kind", kind), ("id", id),
                ("x", rect.Position.X), ("y", rect.Position.Y), ("w", rect.Size.X), ("h", rect.Size.Y),
            };
            fields.AddRange(extra);
            if (Note(node) is { } note)
                fields.Add(("conditional", note));
            return new Entry(rect.Position.Y, rect.Position.X, kind, id ?? "", fields.ToArray());
        }

        Rect2I Anchored(Node2D node, int w, int cap) =>
            AnchoredRect(map, node, w, Math.Max(1, ScanRows(Blocked, AnchorLeft(map, node, w), w,
                BaseRow(Local(map, node).Y), cap, footprints)));

        foreach (Node node in nodes)
        {
            switch (node)
            {
                case Prop prop when FacadeArt(prop) is null:
                {
                    int drawnW = Mathf.RoundToInt(prop.Source.Size.X / MapRoot.TileSize);
                    int drawnH = Mathf.RoundToInt(prop.Source.Size.Y / MapRoot.TileSize);
                    if (prop.TexturePath == FarmBuildings.TexturePath
                        && (prop.Source == FarmBuildings.TreeLeafy || prop.Source == FarmBuildings.TreeBare))
                    {
                        Vector2 p = Local(map, prop);
                        var trunk = new Vector2I(AnchorLeft(map, prop, drawnW) + FarmBuildings.TreeTrunkColumn,
                            BaseRow(p.Y - FarmBuildings.TreeInkGap));
                        props.Add(PropEntry(prop, "tree", new Rect2I(trunk, Vector2I.One),
                            ("variant", prop.Source == FarmBuildings.TreeBare ? "bare" : "leafy"),
                            ("drawnW", drawnW), ("drawnH", drawnH), ("sample", false)));
                        break;
                    }
                    (string kind, object? variant) = TownPropKind(prop);
                    var extra = new List<(string, object?)>();
                    if (variant != null)
                        extra.Add(("variant", variant));
                    extra.Add(("drawnW", drawnW));
                    extra.Add(("drawnH", drawnH));
                    if (kind == "prop")
                    {
                        extra.Add(("texture", prop.TexturePath));
                        extra.Add(("source", RectJson(prop.Source)));
                    }
                    props.Add(PropEntry(prop, kind, Anchored(prop, drawnW, drawnH), extra.ToArray()));
                    break;
                }
                case RoadBarrier chain:
                {
                    Rect2I rect = AnchoredRect(map, chain, chain.TilesWide, 1);
                    Entry entry = PropEntry(chain, "chain", rect);
                    chains.Add((entry, rect));
                    break;
                }
                case PitCover pit:
                    props.Add(PropEntry(pit, "pit_cover", AnchoredRect(map, pit, PitCover.TilesWide, PitCover.TilesTall)));
                    break;
                case DriveInScreen screen:
                {
                    int px = DriveInScreen.FaceHeight + DriveInScreen.LegHeight;
                    props.Add(PropEntry(screen, "screen", Anchored(screen, screen.TilesWide, px / MapRoot.TileSize),
                        ("drawnH", px / (float)MapRoot.TileSize)));
                    break;
                }
                case DriveInSpeaker speaker:
                    props.Add(PropEntry(speaker, "speaker", Anchored(speaker, 1, 1)));
                    break;
                case GuestCar car:
                {
                    // Its real facing and footprint (GuestCar): the lot's cars park
                    // nose-in (N) between a stall's stripes, off the tile grid, so the
                    // exact ground rides along in px; the rect is the tiles it covers.
                    Rect2 f = car.FootprintPx;
                    var px = new Rect2(Local(map, car) + f.Position, f.Size);
                    var first = new Vector2I(Mathf.FloorToInt(px.Position.X / MapRoot.TileSize),
                        Mathf.FloorToInt(px.Position.Y / MapRoot.TileSize));
                    var last = new Vector2I(Mathf.CeilToInt(px.End.X / MapRoot.TileSize),
                        Mathf.CeilToInt(px.End.Y / MapRoot.TileSize));
                    props.Add(PropEntry(car, "car", new Rect2I(first, last - first),
                        ("paint", car.Paint), ("facing", car.Facing), ("footprintPx", RectJson(px))));
                    break;
                }
                case Mailbox box:
                    props.Add(PropEntry(box, "mailbox", new Rect2I(CellOf(map, box), Vector2I.One)));
                    break;
                case ShippingBin bin:
                {
                    Vector2 p = Local(map, bin);
                    var size = new Vector2I(Mathf.RoundToInt(bin.ClosedSource.Size.X / MapRoot.TileSize),
                        Mathf.RoundToInt(bin.ClosedSource.Size.Y / MapRoot.TileSize));
                    var cell = new Vector2I(
                        Mathf.FloorToInt((p.X - bin.ClosedSource.Size.X / 2f) / MapRoot.TileSize),
                        Mathf.FloorToInt((p.Y - bin.ClosedSource.Size.Y / 2f) / MapRoot.TileSize));
                    props.Add(PropEntry(bin, "shipping_bin", new Rect2I(cell, size)));
                    break;
                }
                case StreetLight light:
                {
                    Vector2 p = Local(map, light);
                    Vector2I cell = AnchorCell(p);
                    lights.Add(new Entry(cell.Y, cell.X, "street", NameOf(light) ?? "", Fields(
                        ("kind", "street"), ("id", NameOf(light)), ("x", cell.X), ("y", cell.Y), ("px", PxJson(p)),
                        ("facing", light.ArmLeft ? "W" : "E"), ("lit", light.Lit))));
                    break;
                }
                case GlowLight glow when glow.GetParent() is not StreetLight:
                {
                    Vector2 p = Local(map, glow);
                    Vector2I cell = AnchorCell(p);
                    string? parent = NamedAncestor(glow, map);
                    lights.Add(new Entry(cell.Y, cell.X, "glow", (parent ?? "") + "/" + (NameOf(glow) ?? ""), Fields(
                        ("kind", "glow"), ("id", NameOf(glow)), ("x", cell.X), ("y", cell.Y), ("px", PxJson(p)),
                        ("falloff", glow.Size == GlowLight.Falloff.Large ? "large" : "small"),
                        ("color", glow.Color), ("parent", parent))));
                    break;
                }
                case Sign sign when !sign.DrawPlaceholder:
                    readAreas.Add(sign);
                    break;
                case Sign sign:
                    signs.Add(SignEntry(map, sign, "board", null, sign.Message, Note(sign)));
                    break;
                case GarageSaleSign sale:
                    signs.Add(SignEntry(map, sale, "for_sale", null, null, Note(sale),
                        ("soldLine", Garage.SaleSignSoldLine)));
                    break;
                case PoleSign pole:
                    signs.Add(SignEntry(map, pole, "pole", string.Join("\n", pole.Lines), null, Note(pole),
                        ("lines", pole.Lines), ("face", pole.Face), ("letters", pole.Letters)));
                    break;
                case MotelSign motelSign:
                    signs.Add(SignEntry(map, motelSign, "pole", MotelSign.PanelText, null, Note(motelSign),
                        ("lines", new[] { MotelSign.PanelText, "", MotelSign.VacancyNo + " " + MotelSign.VacancyText }),
                        ("neon", MotelSign.VacancyNo + " " + MotelSign.VacancyText),
                        ("neonDark", MotelSign.VacancyNo), ("blinks", MotelSign.VacancyText[..1]),
                        ("nameplateBlank", true)));
                    break;
                case WallBandSign band:
                    signs.Add(SignEntry(map, band, "wallband", band.Text, null, Note(band),
                        ("lit", band.LitAtNight), ("building", NamedAncestor(band, map))));
                    break;
                case BracketSign bracket:
                    signs.Add(SignEntry(map, bracket, "bracket", bracket.Text, null, Note(bracket),
                        ("building", NamedAncestor(bracket, map))));
                    break;
                case NeonWordSign neon:
                    signs.Add(SignEntry(map, neon, "neon", neon.Word, null, Note(neon),
                        ("building", NamedAncestor(neon, map)), ("onMinutes", OnRanges(neon.OnAt))));
                    break;
            }
        }

        // A read area rides a drawn sign's foot tile: its words become that sign's readText.
        foreach (Sign area in readAreas)
        {
            Vector2I cell = CellOf(map, area);
            int index = signs.FindIndex(s => s.X == cell.X && s.Y == cell.Y && s.Kind == "pole");
            if (index >= 0)
                signs[index] = signs[index].With("readText", area.Message);
            else
                signs.Add(SignEntry(map, area, "read", null, area.Message, Note(area)));
        }

        // A chain's board: the sign standing within one tile of its span.
        foreach ((Entry entry, Rect2I rect) in chains)
        {
            Entry? board = signs.Where(s => s.Kind == "board" && rect.Grow(1).HasPoint(new Vector2I(s.X, s.Y)))
                .Cast<Entry?>().FirstOrDefault();
            props.Add(entry.With("board", board?.Id));
        }

        MapExtras(map, props);

        // Doors.
        var doorEntries = doors.Select(d =>
        {
            Vector2I cell = CellOf(map, d);
            return new Entry(cell.Y, cell.X, "door", d.TargetMapId, Fields(
                ("x", cell.X), ("y", cell.Y), ("to", d.TargetMapId), ("spawn", d.TargetSpawnId),
                ("lockedBy", d.RequiredFlag.Length > 0 ? d.RequiredFlag : null), ("building", BuildingAt(cell))));
        }).ToList();

        // Exits.
        var exits = new List<Entry>();
        foreach (MapExit exit in nodes.OfType<MapExit>())
        {
            Rect2I rect = ExitRect(map, exit);
            int west = rect.Position.X, east = width - rect.End.X;
            int north = rect.Position.Y, south = height - rect.End.Y;
            int nearest = Math.Min(Math.Min(west, east), Math.Min(north, south));
            string edge = nearest == west ? "W" : nearest == east ? "E" : nearest == north ? "N" : "S";
            bool wrap = exit.TargetSpawnId == RoadWrap.ArrivalSpawn;
            string? note = wrap ? WrapNote : Note(exit);
            if (exit.IsEnabled != null && note == null)
                throw new InvalidOperationException(
                    $"WorldDump: gated exit '{exit.Name}' on '{map.MapId}' has no conditional note.");
            Vector2I? arrival = spawns.TryGetValue(exit.TargetMapId, out var there)
                && there.TryGetValue(exit.TargetSpawnId, out Vector2I s) ? s : (Vector2I?)null;
            exits.Add(new Entry(rect.Position.Y, rect.Position.X, "exit", exit.Name, Fields(
                ("id", exit.Name.ToString()), ("edge", edge),
                ("x", rect.Position.X), ("y", rect.Position.Y), ("w", rect.Size.X), ("h", rect.Size.Y),
                ("to", exit.TargetMapId), ("spawn", exit.TargetSpawnId),
                ("spawnX", arrival?.X), ("spawnY", arrival?.Y), ("wrap", wrap), ("conditional", note))));
        }

        var spawnEntries = spawns[map.MapId].Select(kv => new Entry(kv.Value.Y, kv.Value.X, "spawn", kv.Key,
            Fields(("name", kv.Key), ("x", kv.Value.X), ("y", kv.Value.Y)))).ToList();

        // Write.
        sb.Append("    {\n");
        sb.Append("      \"id\": ").Append(MapRecipe.Quote(map.MapId)).Append(",\n");
        sb.Append("      \"width\": ").Append(width).Append(",\n");
        sb.Append("      \"height\": ").Append(height).Append(",\n");
        sb.Append("      \"legend\": ");
        Value(sb, legend.Select(kv => (kv.Key.ToString(), (object?)kv.Value)).ToArray());
        sb.Append(",\n");
        WriteList(sb, "surfaces", surfaceRows.Select(MapRecipe.Quote).ToList(), last: false);
        WriteList(sb, "blocked", blockedRows.Select(MapRecipe.Quote).ToList(), last: false);
        WriteList(sb, "buildings", Sorted(buildings), last: false);
        WriteList(sb, "props", Sorted(props), last: false);
        WriteList(sb, "lights", Sorted(lights), last: false);
        WriteList(sb, "signs", Sorted(signs), last: false);
        WriteList(sb, "doors", Sorted(doorEntries), last: false);
        WriteList(sb, "exits", Sorted(exits), last: false);
        WriteList(sb, "spawns", Sorted(spawnEntries), last: true);
    }

    /// <summary>The solved-in-code geometry no node carries, through the maps' read-only views.</summary>
    private static void MapExtras(MapRoot map, List<Entry> props)
    {
        Entry Plain(string kind, string? id, Rect2I rect, params (string, object?)[] extra) =>
            new(rect.Position.Y, rect.Position.X, kind, id ?? "", Fields(
                new (string, object?)[]
                {
                    ("kind", kind), ("id", id),
                    ("x", rect.Position.X), ("y", rect.Position.Y), ("w", rect.Size.X), ("h", rect.Size.Y),
                }.Concat(extra).ToArray()));

        if (map is ExteriorMap { KerbCuts: { } cuts })
        {
            foreach ((int first, int last) in cuts.North)
                props.Add(Plain("kerb_cut", null, new Rect2I(first, cuts.RoadTop, last - first + 1, 1), ("side", "N")));
            foreach ((int first, int last) in cuts.South)
                props.Add(Plain("kerb_cut", null, new Rect2I(first, cuts.RoadTop + 1, last - first + 1, 1), ("side", "S")));
        }

        if (map is TownMap)
            props.Add(Plain("worn_cobble", null, new Rect2I(TownMap.WornCobble, Vector2I.One)));

        if (map is WestEntryMap { LotStalls: { } stalls })
        {
            (Rect2I lot, IReadOnlyList<Rect2I> stripes) = stalls;
            props.Add(Plain("stall_stripes", null, lot,
                ("stripesPx", stripes.Select(r => (object?)RectJson(r)).ToList())));
        }

        if (map is DriveInMap { FieldRamps: { } ramps })
        {
            (Rect2I field, IReadOnlyList<int> rows) = ramps;
            props.Add(Plain("ramp_rows", null, field, ("rowsPx", rows.Select(y => (object?)y).ToList())));
        }

        if (map is TestMap farm)
        {
            foreach (MapPlacement scatter in farm.Recipe.OfKind(PlacementKinds.Scatter))
                props.Add(Plain("scatter", scatter.Id, new Rect2I(scatter.Cell, Vector2I.One)));

            if (farm.Pen is { } pen)
            {
                props.Add(Plain("fence", "pen", pen,
                    ("gate", farm.PenGate is { } gate ? Fields(("x", gate.X), ("y", gate.Y)) : null),
                    ("gateOpen", farm.PenGate != null)));
            }

            foreach ((Vector2I cell, Vector2I tile) in TestMap.StormDebris)
            {
                string piece = tile == FarmTiles.Log ? "log" : tile == FarmTiles.RockLarge ? "rock" : "other";
                props.Add(Plain("debris", null, new Rect2I(cell, Vector2I.One),
                    ("piece", piece), ("conditional", DebrisNote)));
            }

            var candidates = farm.ObstacleCandidates().Select(c => (c.X, c.Y)).ToList();
            foreach (PlacedObjectRecord obj in ObstacleGen.Generate(candidates, new MapState(), SampleSeed))
            {
                var cell = new Vector2I(obj.X, obj.Y);
                props.Add(obj.ObjectId == ObstacleDefs.Tree
                    ? Plain("tree", null, new Rect2I(cell, Vector2I.One),
                        ("variant", TestMap.IsBareTree(cell) ? "bare" : "leafy"),
                        ("drawnW", FarmBuildings.TreeTiles),
                        ("drawnH", Mathf.RoundToInt(FarmBuildings.TreeLeafy.Size.Y / MapRoot.TileSize)),
                        ("sample", true))
                    : Plain(obj.ObjectId, null, new Rect2I(cell, Vector2I.One), ("sample", true)));
            }
        }
    }

    private static Entry MotelEntry(MapRoot map, MotelFacade motel, List<Door> doors, List<Rect2I> footprints)
    {
        var court = ((WestEntryMap)map).MotorCourt!.Value;
        footprints.Add(court.Office);
        footprints.Add(court.Strip);
        Rect2I all = court.Office.Merge(court.Strip);
        Door? DoorAt(int x) => doors.FirstOrDefault(d => CellOf(map, d) == new Vector2I(x, court.DoorRow));
        Door? office = DoorAt(court.OfficeDoorX);
        Vector2 anchor = Local(map, motel);
        float half = MotelFacade.DrawnWidth / 2f;

        var rooms = new List<object?>();
        for (int i = 0; i < court.RoomDoorX.Count; i++)
        {
            Door? door = DoorAt(court.RoomDoorX[i]);
            rooms.Add(Fields(("room", i + 1),
                ("door", Fields(("x", court.RoomDoorX[i]), ("y", court.DoorRow))),
                ("doorColor", MotelFacade.RoomDoorPaint[i]), ("to", door?.TargetMapId),
                ("lockedBy", door is { RequiredFlag.Length: > 0 } ? door.RequiredFlag : null)));
        }
        var lettering = new List<object?> { MotelFacade.OfficeText };
        lettering.AddRange(Enumerable.Range(1, court.RoomDoorX.Count).Select(n => (object?)n.ToString(CultureInfo.InvariantCulture)));
        lettering.Add(MotelFacade.IceText);

        return new Entry(all.Position.Y, all.Position.X, "building", motel.Name, Fields(
            ("id", motel.Name.ToString()), ("place", office?.TargetMapId), ("art", "motel"),
            ("x", all.Position.X), ("y", all.Position.Y), ("w", all.Size.X), ("h", all.Size.Y),
            ("drawnW", MotelFacade.DrawnWidth / (float)MapRoot.TileSize),
            ("drawnH", MotelFacade.DrawnHeight / (float)MapRoot.TileSize),
            ("wall", null), ("door", DoorJson(map, office)), ("label", null), ("state", null),
            ("anchorPx", PxJson(anchor)),
            ("extentPx", new object?[] { anchor.X - half, anchor.Y - MotelFacade.DrawnHeight, anchor.X + half, anchor.Y }),
            ("parts", new object?[]
            {
                Fields(("part", "office"), ("x", court.Office.Position.X), ("y", court.Office.Position.Y),
                    ("w", court.Office.Size.X), ("h", court.Office.Size.Y),
                    ("door", Fields(("x", court.OfficeDoorX), ("y", court.DoorRow))),
                    ("doorColor", MotelFacade.OfficeDoorPaint), ("text", MotelFacade.OfficeText)),
                Fields(("part", "strip"), ("x", court.Strip.Position.X), ("y", court.Strip.Position.Y),
                    ("w", court.Strip.Size.X), ("h", court.Strip.Size.Y)),
            }),
            ("rooms", rooms), ("lettering", lettering)));
    }

    private static Entry SignEntry(MapRoot map, Node2D node, string kind, string? text, string? readText,
        string? note, params (string, object?)[] extra)
    {
        Vector2 p = Local(map, node);
        Vector2I cell = AnchorCell(p);
        string? id = NameOf(node);
        var fields = new List<(string, object?)>
        {
            ("kind", kind), ("id", id), ("x", cell.X), ("y", cell.Y), ("px", PxJson(p)),
            ("text", text), ("readText", readText),
        };
        fields.AddRange(extra);
        if (note != null)
            fields.Add(("conditional", note));
        return new Entry(cell.Y, cell.X, kind, id ?? "", fields.ToArray());
    }

    // ------------------------------------------------------------------
    // Geometry
    // ------------------------------------------------------------------

    private static Dictionary<string, Vector2I> SpawnsOf(MapRoot map)
    {
        var result = new Dictionary<string, Vector2I>();
        if (map.GetNodeOrNull("Spawns") is { } host)
        {
            foreach (Node child in host.GetChildren())
            {
                if (child is Marker2D marker)
                    result[marker.Name] = CellOf(map, marker);
            }
        }
        return result;
    }

    private static Vector2 Local(MapRoot map, Node2D node) => map.ToLocal(node.GlobalPosition);

    /// <summary>The tile a centred node (area, marker) stands in.</summary>
    private static Vector2I CellOf(MapRoot map, Node2D node)
    {
        Vector2 p = Local(map, node);
        return new Vector2I(Mathf.FloorToInt(p.X / MapRoot.TileSize), Mathf.FloorToInt(p.Y / MapRoot.TileSize));
    }

    /// <summary>The tile a base-anchored node stands on (its Position is the bottom edge).</summary>
    private static Vector2I AnchorCell(Vector2 p) =>
        new(Mathf.FloorToInt(p.X / MapRoot.TileSize), BaseRow(p.Y));

    private static int BaseRow(float y) => Mathf.FloorToInt((y - 1f) / MapRoot.TileSize);

    private static int AnchorLeft(MapRoot map, Node2D node, int w) =>
        Mathf.RoundToInt((Local(map, node).X - w * MapRoot.TileSize / 2f) / MapRoot.TileSize);

    /// <summary>Inverse of <see cref="Prop.Anchor"/>: the footprint rect whose bottom-centre
    /// the node stands on, <paramref name="rows"/> deep.</summary>
    private static Rect2I AnchoredRect(MapRoot map, Node2D node, int w, int rows)
    {
        int bottom = BaseRow(Local(map, node).Y);
        return new Rect2I(AnchorLeft(map, node, w), bottom - rows + 1, w, rows);
    }

    /// <summary>Rows, counted up from <paramref name="bottom"/>, in which every column is
    /// blocked — capped at the drawn height, and stopping at a building's footprint.</summary>
    private static int ScanRows(Func<int, int, bool> blocked, int x0, int w, int bottom, int cap,
        List<Rect2I> stopAt)
    {
        int rows = 0;
        for (int y = bottom; y > bottom - cap && y >= 0; y--)
        {
            for (int x = x0; x < x0 + w; x++)
            {
                var cell = new Vector2I(x, y);
                if (!blocked(x, y) || stopAt.Any(r => r.HasPoint(cell)))
                    return rows;
            }
            rows++;
        }
        return rows;
    }

    private static Rect2I ExitRect(MapRoot map, MapExit exit)
    {
        foreach (Node child in exit.GetChildren())
        {
            if (child is CollisionShape2D { Shape: RectangleShape2D shape } collision)
            {
                Vector2 topLeft = Local(map, exit) + collision.Position - shape.Size / 2f;
                return new Rect2I(
                    Mathf.RoundToInt(topLeft.X / MapRoot.TileSize), Mathf.RoundToInt(topLeft.Y / MapRoot.TileSize),
                    Mathf.RoundToInt(shape.Size.X / MapRoot.TileSize), Mathf.RoundToInt(shape.Size.Y / MapRoot.TileSize));
            }
        }
        throw new InvalidOperationException($"WorldDump: exit '{exit.Name}' on '{map.MapId}' has no rectangle.");
    }

    // ------------------------------------------------------------------
    // Classification
    // ------------------------------------------------------------------

    /// <summary>The handoff-art key of a facade Prop, or null for a plain prop.</summary>
    private static string? FacadeArt(Prop prop) => prop switch
    {
        BarnFacade => "barn",
        StoreFacade => "general_store",
        _ when prop.TexturePath == TownMap.TownHallPath => "town_hall",
        _ when prop.TexturePath == FarmBuildings.TexturePath && prop.Source == FarmBuildings.Farmhouse => "farmhouse",
        _ => null,
    };

    private static (string Kind, object? Variant) TownPropKind(Prop prop)
    {
        if (prop.TexturePath != TownProps.TexturePath)
            return ("prop", null);
        if (prop.Source == TownProps.Well) return ("well", null);
        if (prop.Source == TownProps.BenchA) return ("bench", "a");
        if (prop.Source == TownProps.BenchB) return ("bench", "b");
        if (prop.Source == TownProps.NoticeBoard) return ("notice_board", null);
        int planter = Array.IndexOf(TownProps.Planters, prop.Source);
        return planter >= 0 ? ("planter", planter) : ("prop", null);
    }

    /// <summary>A node's own name, or null when Godot generated it (not stable).</summary>
    private static string? NameOf(Node node)
    {
        string name = node.Name;
        return name.StartsWith('@') ? null : name;
    }

    private static string? NamedAncestor(Node node, MapRoot map)
    {
        for (Node? n = node.GetParent(); n != null && n != map; n = n.GetParent())
        {
            if (NameOf(n) is { } name)
                return name;
        }
        return null;
    }

    private static void Collect(Node node, List<Node> into)
    {
        foreach (Node child in node.GetChildren())
        {
            into.Add(child);
            Collect(child, into);
        }
    }

    /// <summary>The minute-of-day ranges a neon tube is charged, 0 = 6:00.</summary>
    private static List<object?> OnRanges(Func<int, bool> onAt)
    {
        var ranges = new List<object?>();
        int start = -1;
        for (int m = 0; m <= GameTime.MinutesPerDay; m++)
        {
            bool on = m < GameTime.MinutesPerDay && onAt(m);
            if (on && start < 0)
                start = m;
            else if (!on && start >= 0)
            {
                ranges.Add(new object?[] { start, m - 1 });
                start = -1;
            }
        }
        return ranges;
    }

    // ------------------------------------------------------------------
    // JSON (hand-rolled like MapRecipe: fixed field order, one entry per line)
    // ------------------------------------------------------------------

    private static (string, object?)[] Fields(params (string, object?)[] fields) => fields;

    private static object? DoorJson(MapRoot map, Door? door) =>
        door == null ? null : Fields(("x", CellOf(map, door).X), ("y", CellOf(map, door).Y));

    private static object?[] RectJson(Rect2 r) =>
        new object?[] { (int)r.Position.X, (int)r.Position.Y, (int)r.Size.X, (int)r.Size.Y };

    private static object?[] PxJson(Vector2 p) => new object?[] { p.X, p.Y };

    private static List<string> Sorted(List<Entry> entries) =>
        entries.Select(e => (Entry: e, Line: e.Json()))
            .OrderBy(e => e.Entry.Y).ThenBy(e => e.Entry.X)
            .ThenBy(e => e.Entry.Kind, StringComparer.Ordinal).ThenBy(e => e.Entry.Id, StringComparer.Ordinal)
            .ThenBy(e => e.Line, StringComparer.Ordinal)
            .Select(e => e.Line).ToList();

    private static void WriteList(StringBuilder sb, string key, List<string> items, bool last)
    {
        sb.Append("      ").Append(MapRecipe.Quote(key)).Append(": [");
        if (items.Count == 0)
        {
            sb.Append(']');
        }
        else
        {
            sb.Append('\n');
            for (int i = 0; i < items.Count; i++)
                sb.Append("        ").Append(items[i]).Append(i < items.Count - 1 ? ",\n" : "\n");
            sb.Append("      ]");
        }
        sb.Append(last ? "\n" : ",\n");
    }

    internal static void Value(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case string s:
                sb.Append(MapRecipe.Quote(s));
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case int i:
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                break;
            case float f:
                float rounded = MathF.Round(f, 3);
                sb.Append((rounded == 0f ? 0f : rounded).ToString("0.###", CultureInfo.InvariantCulture));
                break;
            case Color c:
                sb.Append(MapRecipe.Quote("#" + c.ToHtml(false)));
                break;
            case (string, object?)[] fields:
                sb.Append('{');
                for (int i = 0; i < fields.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(MapRecipe.Quote(fields[i].Item1)).Append(": ");
                    Value(sb, fields[i].Item2);
                }
                sb.Append('}');
                break;
            case System.Collections.IEnumerable list:
                sb.Append('[');
                bool first = true;
                foreach (object? item in list)
                {
                    if (!first)
                        sb.Append(", ");
                    first = false;
                    Value(sb, item);
                }
                sb.Append(']');
                break;
            default:
                throw new InvalidOperationException($"WorldDump: cannot write a {value.GetType().Name}.");
        }
    }
}
