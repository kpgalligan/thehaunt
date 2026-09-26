using System.Xml.Linq;
using Godot;
using TheHaunt.Content;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;
using FileAccess = Godot.FileAccess;

namespace TheHaunt.Tests;

/// <summary>
/// The Tiled files: the TMX subset reads and writes canonically, a broken file fails
/// loudly naming itself, the optional obstacles layer reads by name, every shipped file
/// still says what its code seed says (the drift tripwire, semantic — Tiled owns the
/// bytes), the town, the road strip and the farm really build from theirs — water,
/// bushes, fences and the pen included — the farm and the exteriors each refuse the
/// other's palette entries, and the palettes on disk are the palettes the loader uses.
/// </summary>
public static class TiledMapTests
{
    [SimTest]
    public static void Tiled_TownSeedRoundTripsThroughTmx(TestContext t)
    {
        TiledMap seed = TownMap.DefaultTiledMap();
        string tmx = seed.ToTmx();
        TiledMap parsed = TiledMap.Parse(tmx, "<round trip>");

        t.AssertEqual(tmx, parsed.ToTmx(), "ToTmx -> Parse -> ToTmx is byte-identical");
        t.AssertEqual(seed.Placements.ToJson(), parsed.Placements.ToJson(), "every placement survives, fields and all");
        t.AssertEqual(MapIds.Town, parsed.MapId, "the map property names the town");

        // The CSV block is one line per map row, Width values each — Tiled's own layout,
        // which is what makes a repainted cell a one-line diff.
        string[] lines = tmx.Split('\n');
        int open = Array.FindIndex(lines, line => line.Trim() == "<data encoding=\"csv\">");
        int close = Array.FindIndex(lines, line => line == "</data>");
        t.Assert(open >= 0 && close > open, "the tmx has a CSV data block");
        t.AssertEqual(seed.Height, close - open - 1, "one CSV line per map row");
        for (int i = open + 1; i < close; i++)
        {
            int values = lines[i].TrimEnd(',').Split(',').Length;
            t.AssertEqual(seed.Width, values, $"CSV line {i - open} holds one gid per column");
        }

        // The value types and escapes the town seed does not exercise.
        var extra = new MapRecipe(MapIds.Town);
        extra.Add(PlacementKinds.Furniture, "till", 3, 4).SetBool(PlacementFields.Blocks, false);
        extra.Add(PlacementKinds.Sign, "odd", 5, 6).SetText(PlacementFields.Text, "Say \"hi\" & <go>\nnow");
        extra.Add(PlacementKinds.ShopCounter, "store", 7, 8).SetInt(PlacementFields.Width, 3);
        extra.Add("future_kind", "thing", 1, 1).SetInt("count", -2);
        var small = new TiledMap(MapIds.Town, new[,] { { "Grass", "Road" }, { "Dirt", "Woods" } }, extra, "<small>");
        TiledMap back = TiledMap.Parse(small.ToTmx(), "<small>");
        t.AssertEqual(small.ToTmx(), back.ToTmx(), "bools, ints, escapes and unknown kinds round-trip");
        t.AssertEqual("Say \"hi\" & <go>\nnow",
            back.Placements.OfKind(PlacementKinds.Sign).Single().Text(PlacementFields.Text), "escaped text reads back verbatim");
        t.AssertEqual("Dirt", back.SurfaceAt(1, 0), "surfaces are indexed [x, y]");
    }

    [SimTest]
    public static void Tiled_UnreadableTmxThrowsNamingTheFile(TestContext t)
    {
        string good = TownMap.DefaultTiledMap().ToTmx();

        string Swap(string from, string to)
        {
            t.Assert(good.Contains(from, StringComparison.Ordinal), $"fixture text '{from}' is in the seed tmx");
            return good.Replace(from, to, StringComparison.Ordinal);
        }

        var broken = new (string Label, string Body)[]
        {
            ("<not xml>", "<map orientation=\"orthogonal\""),
            ("<isometric>", Swap("orientation=\"orthogonal\"", "orientation=\"isometric\"")),
            ("<32px tiles>", Swap("tilewidth=\"16\" tileheight=\"16\"", "tilewidth=\"32\" tileheight=\"32\"")),
            ("<infinite>", Swap("infinite=\"0\"", "infinite=\"1\"")),
            ("<no map property>", Swap("  <property name=\"map\" value=\"town\"/>\n", "")),
            ("<embedded tileset>", Swap("<tileset firstgid=\"1\" source=\"tiled/surfaces.tsx\"/>",
                "<tileset firstgid=\"1\" name=\"x\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"1\" columns=\"1\"/>")),
            ("<other tileset>", Swap("tiled/surfaces.tsx", "tiled/farm.tsx")),
            ("<renamed layer>", Swap("name=\"surface\"", "name=\"Tile Layer 1\"")),
            ("<base64>", Swap("encoding=\"csv\"", "encoding=\"base64\"")),
            ("<short csv>", Swap("<data encoding=\"csv\">\n5,", "<data encoding=\"csv\">\n")),
            ("<empty cell>", Swap("<data encoding=\"csv\">\n5,", "<data encoding=\"csv\">\n0,")),
            ("<gid past palette>", Swap("<data encoding=\"csv\">\n5,", "<data encoding=\"csv\">\n99,")),
            ("<renamed object layer>", Swap("name=\"placements\"", "name=\"Object Layer 1\"")),
            ("<group layer>", Swap(" </objectgroup>\n", " </objectgroup>\n <group id=\"3\" name=\"g\"/>\n")),
            ("<off grid>", Swap("x=\"128\" y=\"176\"", "x=\"130\" y=\"176\"")),
            ("<no class>", Swap("name=\"general_store\" type=\"prop\"", "name=\"general_store\"")),
            ("<float property>", Swap("<property name=\"spawn\" value=\"entry\"/>",
                "<property name=\"spawn\" type=\"float\" value=\"1.5\"/>")),
            ("<reserved property>", Swap("<property name=\"spawn\" value=\"entry\"/>",
                "<property name=\"kind\" value=\"entry\"/>")),
            ("<ellipse>", Swap("type=\"prop\" x=\"128\" y=\"176\" width=\"16\" height=\"16\"/>",
                "type=\"prop\" x=\"128\" y=\"176\" width=\"16\" height=\"16\">\n   <ellipse/>\n  </object>")),
            ("<unknown tileset>", Swap(ObstacleTilesetLine,
                ObstacleTilesetLine + " <tileset firstgid=\"99\" source=\"tiled/farm.tsx\"/>\n")),
            ("<obstacles tileset twice>", Swap(ObstacleTilesetLine, ObstacleTilesetLine + ObstacleTilesetLine)),
            ("<unknown tile layer>", Swap("name=\"obstacles\"", "name=\"Tile Layer 2\"")),
            ("<obstacle in surface>", Swap("<data encoding=\"csv\">\n5,",
                $"<data encoding=\"csv\">\n{TiledObstacles.FirstGid},")),
            ("<surface in obstacles>", Swap(ObstacleDataStart + "0,", ObstacleDataStart + "1,")),
            ("<obstacle past palette>", Swap(ObstacleDataStart + "0,",
                $"{ObstacleDataStart}{TiledObstacles.FirstGid + TiledObstacles.Names.Count},")),
        };

        foreach ((string label, string body) in broken)
        {
            try
            {
                TiledMap.Parse(body, label);
                t.Assert(false, $"{label}: should have thrown");
            }
            catch (MapRecipeException e)
            {
                t.AssertEqual(label, e.FilePath, $"{label}: the exception names the file");
            }
        }

        // The good text itself parses — so each failure above is its own edit's doing.
        t.AssertEqual(MapIds.Town, TiledMap.Parse(good, "<good>").MapId, "the unedited seed reads");
    }

    // The canonical writer's obstacles tileset line and the head of its obstacles CSV.
    private static string ObstacleTilesetLine =>
        $" <tileset firstgid=\"{TiledObstacles.FirstGid}\" source=\"{TiledObstacles.TilesetSource}\"/>\n";

    private const string ObstacleDataStart =
        "name=\"obstacles\" width=\"48\" height=\"30\">\n  <data encoding=\"csv\">\n";

    /// <summary>The seed's text with its obstacles layer (the whole &lt;layer&gt; element) cut out.</summary>
    private static string WithoutObstacleLayer(string tmx)
    {
        int start = tmx.IndexOf(" <layer id=\"3\" name=\"obstacles\"", StringComparison.Ordinal);
        int end = tmx.IndexOf(" </layer>\n", start, StringComparison.Ordinal) + " </layer>\n".Length;
        return tmx[..start] + tmx[end..];
    }

    [SimTest]
    public static void Tiled_ObstacleLayerIsOptionalAndReadsByName(TestContext t)
    {
        string good = TownMap.DefaultTiledMap().ToTmx();
        t.Assert(good.Contains(ObstacleTilesetLine, StringComparison.Ordinal), "the writer emits the obstacles tileset");
        t.Assert(good.Contains(ObstacleDataStart, StringComparison.Ordinal), "the writer emits the obstacles layer");

        void AllEmpty(TiledMap map, string label)
        {
            int filled = 0;
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.ObstacleAt(x, y) != null)
                        filled++;
            t.AssertEqual(0, filled, $"{label}: every obstacle cell is empty");
        }

        // (a) Kevin's current shape: no obstacles tileset, no obstacles layer.
        string bare = WithoutObstacleLayer(good.Replace(ObstacleTilesetLine, "", StringComparison.Ordinal));
        t.Assert(!bare.Contains("obstacles", StringComparison.Ordinal), "(a) the fixture has no obstacles at all");
        AllEmpty(TiledMap.Parse(bare, "<no obstacles>"), "(a) no layer");

        // (b) The layer without its tileset reads while every cell is 0.
        AllEmpty(TiledMap.Parse(good.Replace(ObstacleTilesetLine, "", StringComparison.Ordinal), "<no tileset>"),
            "(b) an all-0 layer without obstacles.tsx");

        // (c) Obstacles round-trip by name. Surfaces stay below Water's gid so (d) can
        // lower the obstacles firstgid under them without a clash.
        var surfaces = new[,] { { "Grass", "Road" }, { "Dirt", "Woods" }, { "Grass", "Gravel" } };
        var obstacles = new string?[,] { { "Fence", null }, { null, "Bush" }, { "Fence", "Fence" } };
        var small = new TiledMap(MapIds.Town, surfaces, obstacles, new MapRecipe(MapIds.Town), "<obstacles>");
        string tmx = small.ToTmx();
        TiledMap back = TiledMap.Parse(tmx, "<obstacles>");
        t.AssertEqual(tmx, back.ToTmx(), "(c) a map with fences and bushes round-trips byte-identically");
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                t.AssertEqual(obstacles[x, y], back.ObstacleAt(x, y), $"(c) obstacle at ({x},{y}) reads back by name");

        // (d) A file saved when the surfaces palette was shorter: the obstacles tileset
        // sits two gids lower, and its cells with it. The gid resolves by firstgid.
        int first = TiledObstacles.FirstGid;
        int dataOpen = tmx.IndexOf("name=\"obstacles\"", StringComparison.Ordinal);
        int csvOpen = tmx.IndexOf("<data encoding=\"csv\">\n", dataOpen, StringComparison.Ordinal)
            + "<data encoding=\"csv\">\n".Length;
        int csvClose = tmx.IndexOf("</data>", csvOpen, StringComparison.Ordinal);
        string lowered = string.Join(",", tmx[csvOpen..csvClose].Split(',').Select(token =>
        {
            string trimmed = token.Trim();
            int gid = int.Parse(trimmed);
            return gid == 0 ? token : token.Replace(trimmed, (gid - 2).ToString(), StringComparison.Ordinal);
        }));
        string older = tmx[..csvOpen] + lowered + tmx[csvClose..];
        older = older.Replace($"<tileset firstgid=\"{first}\"", $"<tileset firstgid=\"{first - 2}\"", StringComparison.Ordinal);
        t.Assert(older != tmx, "(d) the fixture really moved the obstacles gids");
        TiledMap shifted = TiledMap.Parse(older, "<shifted>");
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                t.AssertEqual(obstacles[x, y], shifted.ObstacleAt(x, y), $"(d) obstacle at ({x},{y}) reads through the lower firstgid");
                t.AssertEqual(surfaces[x, y], shifted.SurfaceAt(x, y), $"(d) surface at ({x},{y}) is unchanged");
            }
        }
    }

    /// <summary>
    /// Placements only, since 2026-09-26: Kevin reshaped the woods in Tiled — a
    /// deliberate edit, so under this guard's own DECIDE rule the town's ground has left
    /// its code seed behind and the per-cell surface check went with it. The size and
    /// the placements are still held to the seed.
    /// </summary>
    [SimTest]
    public static void Town_ShippedTmxPlacementsMatchTheCodeSeed(TestContext t)
    {
        string path = TiledMapFile.PathFor(MapIds.Town);
        t.Assert(FileAccess.FileExists(path), $"{path} ships with the game");

        TiledMap shipped = TiledMapFile.ReadFrom(path, MapIds.Town);
        TiledMap seed = TownMap.DefaultTiledMap();

        // SEMANTIC, not byte-for-byte: Tiled rewrites the file's bytes (version stamps,
        // attribute order) on every save, so only what the map MEANS is held here.
        const string decide =
            "If this fails, DECIDE which one is right: a code change to the defaults " +
            "(TownMap.DefaultRecipe / BuildDefaultSurfaces) means the file must follow — " +
            "delete it and re-run --seed-tiled town; a deliberate edit in Tiled means the town " +
            "has left its seed behind, and the seed (with this guard) should go with it. " +
            "Never quietly re-seed over a hand-edited map.";

        t.AssertEqual(seed.Width, shipped.Width, $"{path} is the town's width. {decide}");
        t.AssertEqual(seed.Height, shipped.Height, $"{path} is the town's height. {decide}");
        t.AssertEqual(TownMap.DefaultRecipe().ToJson(), shipped.Placements.ToJson(),
            $"{path} placements still match TownMap.DefaultRecipe(). {decide}");
    }

    [SimTest]
    public static async Task Town_BuildsFromItsShippedTmx(TestContext t)
    {
        SaveService.Instance.NewGame();
        string path = TiledMapFile.PathFor(MapIds.Town);
        MapRecipe recipe = TiledMapFile.ReadFrom(path, MapIds.Town).Placements;

        var map = new TownMap();
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            t.AssertEqual(path, map.RecipeSource, "the town built itself from the shipped tmx");

            foreach (MapPlacement spawn in recipe.OfKind(PlacementKinds.Spawn))
            {
                t.Assert(map.GetNodeOrNull<Marker2D>($"Spawns/{spawn.Id}") != null,
                    $"spawn '{spawn.Id}' is a marker travel can ask for");
            }
            foreach (MapPlacement sign in recipe.OfKind(PlacementKinds.Sign))
            {
                var post = map.GetNodeOrNull<Sign>(sign.Id);
                t.Assert(post != null, $"sign '{sign.Id}' is in the scene under its own name");
                t.AssertEqual(Town.SignTextFor(sign.Id) ?? sign.Text(PlacementFields.Text), post!.Message,
                    $"sign '{sign.Id}' carries the place's copy (or the file's, unpromoted)");
            }
            t.AssertEqual(Town.StoreSignText, map.GetNodeOrNull<Sign>("StoreSign")?.Message,
                "the store's board reads the store's hours");

            t.Assert(map.GetNodeOrNull<Prop>("TownHall") != null, "the hall facade keeps its node name");
            t.Assert(map.GetNodeOrNull<StoreFacade>("GeneralStore") != null, "the store facade keeps its node name");

            int doors = map.GetChildren().OfType<Door>().Count();
            int exits = map.GetChildren().OfType<MapExit>().Count();
            t.AssertEqual(recipe.OfKind(PlacementKinds.Door).Count(), doors, "every door in the file is a door in the scene");
            t.AssertEqual(recipe.OfKind(PlacementKinds.Exit).Count(), exits, "every exit in the file is an exit in the scene");

            t.Assert(map.KerbCuts is { } cuts
                    && cuts.RoadTop == 14
                    && cuts.North.SequenceEqual(new[] { (11, 11), (23, 24) })
                    && cuts.South.SequenceEqual(new[] { (24, 24) }),
                "kerb cuts derive from the surface grid: the two door paths north, the plaza path south");
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    /// <summary>Every Tiled map with a code seed but the town (whose ground Kevin has
    /// hand-edited — see <see cref="Town_ShippedTmxPlacementsMatchTheCodeSeed"/>): the road
    /// strip and the farm.</summary>
    private static List<string> SeededExteriors() =>
        WorldDump.ExteriorIds().Where(id => id != MapIds.Town && TiledSeeds.Has(id)).ToList();

    /// <summary>
    /// The road strip's and the farm's shipped files still say, cell for cell and
    /// placement for placement, what their code seeds say — the full drift guard the town
    /// had before its hand edit. SEMANTIC, not byte-for-byte: Tiled owns the bytes.
    /// </summary>
    [SimTest]
    public static void Exteriors_ShippedTmxMatchTheirCodeSeeds(TestContext t)
    {
        List<string> ids = SeededExteriors();
        t.Assert(ids.Count >= 7, $"the road strip's six exteriors and the farm are Tiled maps (found {ids.Count})");

        foreach (string id in ids)
        {
            string path = TiledMapFile.PathFor(id);
            string decide =
                $"If this fails, DECIDE which one is right: a code change to {id}'s defaults " +
                $"(BuildDefaultSurfaces / BuildDefaultRecipe) means the file must follow — delete it " +
                $"and re-run --seed-tiled {id}; a deliberate edit in Tiled means the map has left its " +
                "seed behind, and this guard should say so for it. Never quietly re-seed over a hand-edited map.";
            t.Assert(FileAccess.FileExists(path), $"{path} ships with the game. {decide}");
            if (!FileAccess.FileExists(path))
                continue;

            TiledMap shipped = TiledMapFile.ReadFrom(path, id);
            TiledMap seed = TiledSeeds.For(id);

            t.AssertEqual(seed.Width, shipped.Width, $"{path} is {id}'s width. {decide}");
            t.AssertEqual(seed.Height, shipped.Height, $"{path} is {id}'s height. {decide}");
            if (seed.Width == shipped.Width && seed.Height == shipped.Height)
            {
                string? surface = null, obstacle = null;
                for (int y = 0; y < seed.Height; y++)
                {
                    for (int x = 0; x < seed.Width; x++)
                    {
                        if (surface == null && seed.SurfaceAt(x, y) != shipped.SurfaceAt(x, y))
                            surface = $"({x},{y}): seed {seed.SurfaceAt(x, y)}, file {shipped.SurfaceAt(x, y)}";
                        if (obstacle == null && seed.ObstacleAt(x, y) != shipped.ObstacleAt(x, y))
                            obstacle = $"({x},{y}): seed {seed.ObstacleAt(x, y) ?? "none"}, file {shipped.ObstacleAt(x, y) ?? "none"}";
                    }
                }
                t.Assert(surface == null, $"{path} surfaces match the seed — first difference {surface}. {decide}");
                t.Assert(obstacle == null, $"{path} obstacles match the seed — first difference {obstacle}. {decide}");
            }
            t.AssertEqual(seed.Placements.ToJson(), shipped.Placements.ToJson(),
                $"{path} placements still match the code seed. {decide}");

            string tmx = seed.ToTmx();
            t.AssertEqual(tmx, TiledMap.Parse(tmx, $"<{id} round trip>").ToTmx(),
                $"{id}'s seed: ToTmx -> Parse -> ToTmx is byte-identical");
        }
    }

    /// <summary>The road strip builds from its shipped files: provenance, every placed
    /// spawn, sign, door and exit a node, and the kerb cuts the files ask for.</summary>
    [SimTest]
    public static async Task Exteriors_BuildFromTheirShippedTmx(TestContext t)
    {
        SaveService.Instance.NewGame();
        var kerbs = new Dictionary<string, ((int, int)[] North, (int, int)[] South)?>
        {
            [MapIds.WestEntry] = (new[] { (9, 12) }, new[] { (26, 27), (34, 35) }),
            [MapIds.EastEntry] = (Array.Empty<(int, int)>(), new[] { (33, 34) }),
            [MapIds.DriveIn] = null,
        };

        foreach (string id in SeededExteriors())
        {
            if (id == MapIds.Farm)
                continue;   // its own build — Farm_BuildsFromItsShippedTmx
            string path = TiledMapFile.PathFor(id);
            MapRecipe recipe = TiledMapFile.ReadFrom(path, id).Placements;
            MapRoot root = MapRegistry.Create(id);
            t.Host.AddChild(root);
            await t.WaitFrames(1);
            try
            {
                var map = (ExteriorMap)root;
                t.AssertEqual(path, map.RecipeSource, $"{id} built itself from the shipped tmx");

                foreach (MapPlacement spawn in recipe.OfKind(PlacementKinds.Spawn))
                {
                    t.Assert(map.GetNodeOrNull<Marker2D>($"Spawns/{spawn.Id}") != null,
                        $"{id}: spawn '{spawn.Id}' is a marker travel can ask for");
                }
                foreach (MapPlacement sign in recipe.OfKind(PlacementKinds.Sign))
                {
                    var post = map.GetNodeOrNull<Sign>(sign.Id);
                    t.Assert(post != null, $"{id}: sign '{sign.Id}' is in the scene under its own name");
                    t.Assert(post is { Message.Length: > 0 }, $"{id}: sign '{sign.Id}' has words");
                }
                foreach (MapPlacement door in recipe.OfKind(PlacementKinds.Door))
                {
                    t.AssertEqual(door.Id, map.GetNodeOrNull<Door>($"Door_{door.Id}")?.TargetMapId,
                        $"{id}: door '{door.Id}' is a Door leading there");
                }
                foreach (MapPlacement exit in recipe.OfKind(PlacementKinds.Exit))
                {
                    t.AssertEqual(exit.Id, map.GetNodeOrNull<MapExit>($"Exit_{exit.Id}")?.TargetMapId,
                        $"{id}: exit '{exit.Id}' is a MapExit leading there");
                }

                if (kerbs.TryGetValue(id, out var expected))
                {
                    if (expected is not { } cuts)
                    {
                        t.Assert(map.KerbCuts == null, $"{id} has no road, so no kerb cuts");
                    }
                    else
                    {
                        t.Assert(map.KerbCuts is { } drawn
                                && drawn.RoadTop == 14
                                && drawn.North.SequenceEqual(cuts.North)
                                && drawn.South.SequenceEqual(cuts.South),
                            $"{id}: kerb cuts are the file's kerb_cuts plus the made ground at the gutter");
                    }
                }
            }
            finally
            {
                root.Free();
                await t.WaitFrames(1);
            }
        }
        SaveService.Instance.NewGame();
    }

    /// <summary>The farm builds from its shipped file: provenance, every placed spawn,
    /// sign, door and its one exit a node, and the pen — its box, its gate, and every
    /// rail piece — from the file's fences.</summary>
    [SimTest]
    public static async Task Farm_BuildsFromItsShippedTmx(TestContext t)
    {
        SaveService.Instance.NewGame();
        string path = TiledMapFile.PathFor(MapIds.Farm);
        MapRecipe recipe = TiledMapFile.ReadFrom(path, MapIds.Farm).Placements;

        var map = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            t.AssertEqual(path, map.RecipeSource, "the farm built itself from the shipped tmx");

            foreach (MapPlacement spawn in recipe.OfKind(PlacementKinds.Spawn))
            {
                t.Assert(map.GetNodeOrNull<Marker2D>($"Spawns/{spawn.Id}") != null,
                    $"spawn '{spawn.Id}' is a marker travel can ask for");
            }
            foreach (MapPlacement sign in recipe.OfKind(PlacementKinds.Sign))
            {
                var post = map.GetNodeOrNull<Sign>($"Interactables/{sign.Id}");
                t.Assert(post != null, $"sign '{sign.Id}' is in the scene under its own name");
                t.AssertEqual(Farm.SignTextFor(sign.Id) ?? sign.Text(PlacementFields.Text), post!.Message,
                    $"sign '{sign.Id}' carries the place's copy (or the file's, unpromoted)");
            }

            var doors = new List<Door>();
            var exits = new List<MapExit>();
            CollectFarm(map, doors, exits);
            t.AssertEqual(recipe.OfKind(PlacementKinds.Door).Count(), doors.Count,
                "every door in the file is a door in the scene");
            foreach (MapPlacement door in recipe.OfKind(PlacementKinds.Door))
            {
                t.Assert(
                    doors.Any(node => node.TargetMapId == door.Id
                        && node.TargetSpawnId == door.Text(PlacementFields.Spawn, "default")
                        && node.Position == new Vector2(
                            door.X * MapRoot.TileSize + 8, door.Y * MapRoot.TileSize + 8)),
                    $"the door to '{door.Id}' leads where the file says, from the cell it says");
            }
            t.AssertEqual(1, exits.Count, "the farm has one exit");
            t.AssertEqual(MapIds.Fork, exits.FirstOrDefault()?.TargetMapId, "leading south to the fork");

            t.AssertEqual(new Rect2I(4, 23, 12, 4), map.Pen, "the pen is the box the file's fences draw");
            t.AssertEqual(new Vector2I(9, 26), map.PenGate, "and its gate is the file's gate cell");

            Vector2I Expected(int x, int y) => (x, y) switch
            {
                (4, 23) => FarmTiles.FenceCornerSe,
                (15, 23) => FarmTiles.FenceCornerSw,
                (4, 26) => FarmTiles.FenceCornerNe,
                (15, 26) => FarmTiles.FenceCornerNw,
                (9, 26) => FarmTiles.GateOpen,
                (_, 23 or 26) => FarmTiles.FenceH,
                _ => FarmTiles.FenceV,
            };
            var ground = map.GetNode<TileMapLayer>("Ground");
            for (int y = 23; y <= 26; y++)
            {
                for (int x = 4; x <= 15; x++)
                {
                    if (y is not (23 or 26) && x is not (4 or 15))
                        continue;   // the pen's inside
                    var cell = new Vector2I(x, y);
                    t.AssertEqual(FarmTerrain.FarmSource, ground.GetCellSourceId(cell),
                        $"the pen cell {cell} paints from the farm sheet");
                    t.AssertEqual(FarmTiles.ForAct(Expected(x, y), FarmTiles.Act.One), ground.GetCellAtlasCoords(cell),
                        $"the pen cell {cell} is the piece its neighbours pick");
                }
            }
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    private static void CollectFarm(Node node, List<Door> doors, List<MapExit> exits)
    {
        if (node is Door door)
            doors.Add(door);
        if (node is MapExit exit)
            exits.Add(exit);
        foreach (Node child in node.GetChildren())
            CollectFarm(child, doors, exits);
    }

    /// <summary>
    /// The shared palette carries both sides' entries; each side refuses the other's, by
    /// file. The cases run one at a time: a dev file is keyed by the map id it names
    /// (<see cref="TiledMapFile.UseDevFile"/>), so a second file for the same id would
    /// replace the first.
    /// </summary>
    [SimTest]
    public static void Tiled_FarmAndExteriorsRefuseEachOthersPaletteEntries(TestContext t)
    {
        var cases = new (string Label, string MapId, Vector2I Cell, string? Surface, string? Obstacle, Func<MapRoot> Create)[]
        {
            ("the farm with Grass", MapIds.Farm, new Vector2I(20, 20), "Grass", null,
                () => new TestMap { MapId = MapIds.Farm }),
            ("the farm with a Bush", MapIds.Farm, new Vector2I(20, 20), null, "Bush",
                () => new TestMap { MapId = MapIds.Farm }),
            ("the fork with Pasture", MapIds.Fork, new Vector2I(5, 5), "Pasture", null,
                () => new ForkMap { MapId = MapIds.Fork }),
            ("the fork with a Gate", MapIds.Fork, new Vector2I(5, 5), null, "Gate",
                () => new ForkMap { MapId = MapIds.Fork }),
        };

        try
        {
            for (int i = 0; i < cases.Length; i++)
            {
                (string label, string mapId, Vector2I at, string? surface, string? obstacle, Func<MapRoot> create) = cases[i];
                string path = $"{DevFolder}refuse_{i}.tmx";
                MapRoot? map = null;
                try
                {
                    TiledMap seed = TiledSeeds.For(mapId);
                    var surfaces = new string[seed.Width, seed.Height];
                    var obstacles = new string?[seed.Width, seed.Height];
                    for (int y = 0; y < seed.Height; y++)
                    {
                        for (int x = 0; x < seed.Width; x++)
                        {
                            surfaces[x, y] = seed.SurfaceAt(x, y);
                            obstacles[x, y] = seed.ObstacleAt(x, y);
                        }
                    }
                    if (surface != null)
                        surfaces[at.X, at.Y] = surface;
                    if (obstacle != null)
                        obstacles[at.X, at.Y] = obstacle;

                    TiledMapFile.WriteText(path, new TiledMap(mapId, surfaces, obstacles, seed.Placements, path).ToTmx());
                    TiledMapFile.UseDevFile(path);

                    map = create();
                    try
                    {
                        map._Ready();
                        t.Assert(false, $"{label}: should have thrown");
                    }
                    catch (MapRecipeException e)
                    {
                        t.AssertEqual(path, e.FilePath, $"{label}: refused, naming the file");
                    }
                }
                finally
                {
                    TiledMapFile.ClearDevFiles();
                    if (FileAccess.FileExists(path))
                        DirAccess.RemoveAbsolute(path);
                    map?.Free();
                }
            }
        }
        finally
        {
            DirAccess.RemoveAbsolute(DevFolder);
        }
    }

    private const string DevFolder = "user://test_tiled/";

    [SimTest]
    public static async Task Town_PaintsWaterBushesAndFencesFromItsTmx(TestContext t)
    {
        var water = new[] { new Vector2I(30, 17), new(31, 17), new(32, 17), new(33, 17), new(30, 18), new(33, 18) };
        var deep = new[] { new Vector2I(31, 18), new(32, 18) };
        var fences = new (Vector2I Cell, Vector2I Piece)[]
        {
            (new(35, 17), FarmTiles.FenceCornerSe), (new(36, 17), FarmTiles.FenceH), (new(37, 17), FarmTiles.FenceH),
            (new(35, 18), FarmTiles.FenceV), (new(35, 19), FarmTiles.FenceV), (new(37, 20), FarmTiles.FencePost),
        };
        var bushes = new[] { new Vector2I(30, 20), new(31, 20) };

        TiledMap seed = TownMap.DefaultTiledMap();
        var surfaces = new string[seed.Width, seed.Height];
        var obstacles = new string?[seed.Width, seed.Height];
        for (int y = 0; y < seed.Height; y++)
            for (int x = 0; x < seed.Width; x++)
                surfaces[x, y] = seed.SurfaceAt(x, y);
        foreach (Vector2I cell in water)
            surfaces[cell.X, cell.Y] = "Water";
        foreach (Vector2I cell in deep)
            surfaces[cell.X, cell.Y] = "DeepWater";
        foreach ((Vector2I cell, _) in fences)
            obstacles[cell.X, cell.Y] = "Fence";
        foreach (Vector2I cell in bushes)
            obstacles[cell.X, cell.Y] = "Bush";

        string path = DevFolder + "town.tmx";
        SaveService.Instance.NewGame();
        TownMap? map = null;
        try
        {
            TiledMapFile.WriteText(path,
                new TiledMap(MapIds.Town, surfaces, obstacles, seed.Placements, path).ToTmx());
            TiledMapFile.UseDevFile(path);

            map = new TownMap();
            t.Host.AddChild(map);
            await t.WaitFrames(1);

            t.AssertEqual(path, map.RecipeSource, "the town built itself from the dev file");

            var ground = map.GetNode<TileMapLayer>("Ground");
            foreach ((Vector2I[] cells, Vector2I[] tiles, string label) in
                new[] { (water, LandscapeTiles.Water, "water"), (deep, LandscapeTiles.DeepWater, "deep water") })
            {
                foreach (Vector2I cell in cells)
                {
                    t.AssertEqual(RoadsideTerrain.LandscapeSourceId, ground.GetCellSourceId(cell),
                        $"{label} at {cell} paints from the landscape source");
                    t.Assert(tiles.Contains(ground.GetCellAtlasCoords(cell)),
                        $"{label} at {cell} is a {label} tile, got {ground.GetCellAtlasCoords(cell)}");
                }
            }

            var layer = map.GetNode<TileMapLayer>("Obstacles");
            foreach (Vector2I cell in bushes)
            {
                t.AssertEqual(RoadsideTerrain.LandscapeSourceId, layer.GetCellSourceId(cell),
                    $"the bush at {cell} paints from the landscape source");
                t.Assert(LandscapeTiles.Bush.Contains(layer.GetCellAtlasCoords(cell)),
                    $"the bush at {cell} is a bush tile, got {layer.GetCellAtlasCoords(cell)}");
            }
            foreach ((Vector2I cell, Vector2I piece) in fences)
            {
                t.AssertEqual(RoadsideTerrain.FenceSourceId, layer.GetCellSourceId(cell),
                    $"the fence at {cell} paints from the farm fence source");
                t.AssertEqual(piece, layer.GetCellAtlasCoords(cell), $"the fence at {cell} is the piece its neighbours pick");
            }

            foreach (Vector2I cell in water.Concat(deep).Concat(fences.Select(f => f.Cell)).Concat(bushes))
                t.Assert(!map.IsStandable(cell), $"{cell} is painted, so it blocks");
            t.Assert(map.IsStandable(new Vector2I(34, 18)), "the grass between the pond and the fence stays walkable");
        }
        finally
        {
            TiledMapFile.ClearDevFiles();
            if (FileAccess.FileExists(path))
                DirAccess.RemoveAbsolute(path);
            DirAccess.RemoveAbsolute(DevFolder);
            map?.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    [SimTest]
    public static void Tiled_SurfaceTilesetMatchesThePalette(TestContext t) =>
        AssertTilesetMatches(t, TiledSurfaces.TilesetPath, TiledSurfaces.SurfaceProperty, TiledSurfaces.Names);

    [SimTest]
    public static void Tiled_ObstacleTilesetMatchesThePalette(TestContext t) =>
        AssertTilesetMatches(t, TiledObstacles.TilesetPath, TiledObstacles.ObstacleProperty, TiledObstacles.Names);

    private static void AssertTilesetMatches(TestContext t, string path, string property, IReadOnlyList<string> names)
    {
        const string fix = "The palette files are derived — rerun `godot-mono --headless --path . -- --seed-tiled town` " +
            "to regenerate them (it never overwrites town.tmx).";
        t.Assert(FileAccess.FileExists(path), $"{path} ships beside the town's tmx. {fix}");

        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        t.Assert(file != null, $"{path} opens. {fix}");
        XElement tileset = XDocument.Parse(file!.GetAsText()).Root!;

        t.AssertEqual(names.Count.ToString(), tileset.Attribute("tilecount")?.Value,
            $"{path} has one tile per {property}. {fix}");
        List<XElement> tiles = tileset.Elements("tile").ToList();
        t.AssertEqual(names.Count, tiles.Count, $"{path} names every tile. {fix}");
        for (int i = 0; i < names.Count; i++)
        {
            XElement? tile = tiles.FirstOrDefault(tile => tile.Attribute("id")?.Value == i.ToString());
            string? value = tile?.Element("properties")?.Elements("property")
                .FirstOrDefault(p => p.Attribute("name")?.Value == property)
                ?.Attribute("value")?.Value;
            t.AssertEqual(names[i], value, $"{path} tile {i} is the palette's {property} {i}. {fix}");
        }
    }
}
