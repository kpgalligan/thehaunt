using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.Tests;

public static class SaveTests
{
    [SimTest]
    public static void Save_RoundTrip(TestContext t)
    {
        var data = new GameData { TotalMinutes = 777 };
        data.Player.X = 33.5f;
        data.Player.Y = -12.25f;
        data.Player.Facing = 1;
        data.Player.HasPosition = true;
        MapState map = data.GetMap("m");
        map.SetTile(new TileRecord { X = 1, Y = 2, Kind = "tilled", CropId = "parsnip", GrowthDay = 3, LastWateredDay = 4 });
        map.SetTile(new TileRecord { X = -5, Y = 6, Kind = "tilled" });
        map.SetTile(new TileRecord { X = 7, Y = 8, Kind = "tilled", LastWateredDay = 12 });
        map.Objects.Add(new PlacedObjectRecord { X = 9, Y = 10, ObjectId = "chest" });

        string json = JsonSerializer.Serialize(data, SaveJsonContext.Default.GameData);
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.AssertEqual(777L, loaded.TotalMinutes, "TotalMinutes");
            t.AssertEqual(33.5f, loaded.Player.X, "player X");
            t.AssertEqual(-12.25f, loaded.Player.Y, "player Y");
            t.AssertEqual(1, loaded.Player.Facing, "player facing");
            t.AssertEqual(true, loaded.Player.HasPosition, "player HasPosition");
            t.Assert(loaded.Maps.ContainsKey("m"), "map 'm' present");

            MapState loadedMap = loaded.Maps["m"];
            t.AssertEqual(3, loadedMap.Tiles.Count, "tile count");
            TileRecord? tile = loadedMap.GetTile(1, 2);
            t.Assert(tile != null, "GetTile(1,2) after load (index rebuilt)");
            t.AssertEqual("tilled", tile!.Kind, "tile (1,2) kind");
            t.AssertEqual("parsnip", tile.CropId, "tile (1,2) crop");
            t.AssertEqual(3, tile.GrowthDay, "tile (1,2) growth day");
            t.AssertEqual(4L, tile.LastWateredDay, "tile (1,2) last watered day");
            t.Assert(loadedMap.GetTile(-5, 6) != null, "GetTile(-5,6) after load");
            t.AssertEqual(12L, loadedMap.GetTile(7, 8)!.LastWateredDay, "tile (7,8) last watered day");

            t.AssertEqual(1, loadedMap.Objects.Count, "object count");
            t.AssertEqual("chest", loadedMap.Objects[0].ObjectId, "object id");
            t.AssertEqual(9, loadedMap.Objects[0].X, "object X");
            t.AssertEqual(10, loadedMap.Objects[0].Y, "object Y");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_TooNewRefused(TestContext t)
    {
        SaveService service = SaveService.Instance;
        GameData before = service.Current;
        try
        {
            bool threw = false;
            try
            {
                service.DeserializeFrom("""{"SaveVersion":999,"TotalMinutes":0}""");
            }
            catch (SaveTooNewException e)
            {
                threw = true;
                t.AssertEqual(999, e.FileVersion, "exception FileVersion");
            }
            t.Assert(threw, "DeserializeFrom threw SaveTooNewException");
            t.Assert(ReferenceEquals(before, service.Current), "Current reference unchanged");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_MigrationChainApplies(TestContext t)
    {
        var migration = new MarkerMigration();
        JsonNode root = JsonNode.Parse("""{"SaveVersion":0,"Marker":"original"}""")!;

        JsonNode result = SaveMigrations.Apply(root, new ISaveMigration[] { migration }, currentVersion: 1);

        t.Assert(migration.Applied, "migration ran");
        t.AssertEqual("migrated", (string?)result["Marker"], "mutated field");
        t.AssertEqual(1, result["SaveVersion"]!.GetValue<int>(), "SaveVersion bumped");
    }

    [SimTest]
    public static void Save_FixtureV1Loads(TestContext t)
    {
        using var file = Godot.FileAccess.Open(
            "res://src/Tests/fixtures/v1_minimal.json", Godot.FileAccess.ModeFlags.Read);
        t.Assert(file != null, $"fixture opens: {Godot.FileAccess.GetOpenError()}");
        string json = file!.GetAsText();

        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.AssertEqual(100f, loaded.Player.X, "player X");
            t.AssertEqual(120f, loaded.Player.Y, "player Y");
            t.AssertEqual(2, loaded.Player.Facing, "player facing");
            t.Assert(loaded.Maps.ContainsKey("test_farm"), "map 'test_farm' present");
            TileRecord? tile = loaded.Maps["test_farm"].GetTile(3, 4);
            t.Assert(tile != null, "tile (3,4) survived");
            t.AssertEqual("tilled", tile!.Kind, "tile kind");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_NoGodotTypesInDtos(TestContext t)
    {
        var offenders = new List<string>();
        Walk(typeof(GameData), new HashSet<Type>(), offenders);
        t.Assert(offenders.Count == 0, $"Godot types in save DTO graph: {string.Join(", ", offenders)}");
    }

    [SimTest]
    public static void Save_PerfBudget(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            MapState map = service.Current.GetMap("perf");
            for (int i = 0; i < 5000; i++)
            {
                map.SetTile(new TileRecord
                {
                    X = i % 100,
                    Y = i / 100,
                    Kind = "tilled",
                    GrowthDay = i % 7,
                    LastWateredDay = i % 11,
                });
            }

            // Best-of-3: a single wall-clock sample can flake on a GC pause or a loaded
            // machine; a genuine perf regression slows every run.
            long best = long.MaxValue;
            string json = "";
            for (int run = 0; run < 3; run++)
            {
                var stopwatch = Stopwatch.StartNew();
                json = service.SerializeToString();
                stopwatch.Stop();
                best = Math.Min(best, stopwatch.ElapsedMilliseconds);
            }

            t.Assert(json.Length > 0, "serialized output non-empty");
            t.Assert(best < 100,
                $"SerializeToString best-of-3 took {best} ms (budget 100 ms)");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_AtomicFileWritten(TestContext t)
    {
        SaveService service = SaveService.Instance;
        string path = Path.Combine(SaveService.SaveDirectory, "test_slot_a.json");
        string tmpPath = path + ".tmp";
        try
        {
            // Sentinel Save() is guaranteed to persist (it copies Clock time into the data),
            // so loading the file back proves the disk bytes are real, not just present.
            Clock.Instance.SetTime(new GameTime(4242));
            t.Assert(service.Save("test_slot_a"), "Save returned true");
            t.Assert(File.Exists(path), "test_slot_a.json exists");
            t.Assert(!File.Exists(tmpPath), "no .tmp file remains");
            t.AssertEqual(LoadResult.Ok, service.Load("test_slot_a"), "written file loads back");
            t.AssertEqual(4242L, service.Current.TotalMinutes, "saved TotalMinutes round-trips from disk");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_LoadFailureQuarantines(TestContext t)
    {
        SaveService service = SaveService.Instance;
        string dir = SaveService.SaveDirectory;
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "test_slot_q.json");
        string badPath = path + ".bad";
        string tooNewPath = path + ".toonew";
        try
        {
            // Corrupt file: refused, quarantined, original bytes preserved.
            File.WriteAllText(path, "{not json");
            GameData before = service.Current;
            t.AssertEqual(LoadResult.Corrupt, service.Load("test_slot_q"), "corrupt load result");
            t.Assert(ReferenceEquals(before, service.Current), "Current untouched by corrupt load");
            t.Assert(!File.Exists(path), "corrupt file no longer occupies the slot");
            t.Assert(File.Exists(badPath), "corrupt file quarantined as .bad");
            t.AssertEqual("{not json", File.ReadAllText(badPath), "quarantined bytes intact");

            // Slot is now free: a later Save writes fresh data without touching the quarantine.
            t.Assert(service.Save("test_slot_q"), "save to the freed slot succeeds");
            t.Assert(File.Exists(path), "fresh save written to the slot");
            t.AssertEqual("{not json", File.ReadAllText(badPath), "quarantine survives a later save");
            File.Delete(path);

            // Too-new file: distinct result, distinct quarantine suffix.
            File.WriteAllText(path, """{"SaveVersion":999,"TotalMinutes":0}""");
            t.AssertEqual(LoadResult.TooNew, service.Load("test_slot_q"), "too-new load result");
            t.Assert(File.Exists(tooNewPath), "too-new file quarantined as .toonew");

            // Missing file.
            t.AssertEqual(LoadResult.NoFile, service.Load("test_slot_q"), "missing file load result");
        }
        finally
        {
            foreach (string leftover in Directory.GetFiles(dir, "test_slot_q*"))
            {
                File.Delete(leftover);
            }
            service.NewGame();
        }
    }

    // Recurse into generic type arguments everywhere, and into public instance
    // properties of our own DTO types; flag anything from the GodotSharp assembly.
    private static void Walk(Type type, HashSet<Type> visited, List<string> offenders)
    {
        if (!visited.Add(type))
        {
            return;
        }
        if (type.Assembly.GetName().Name == "GodotSharp")
        {
            offenders.Add(type.FullName ?? type.Name);
            return;
        }
        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                Walk(argument, visited, offenders);
            }
        }
        if (type.Assembly != typeof(GameData).Assembly)
        {
            return;
        }
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Walk(property.PropertyType, visited, offenders);
        }
    }

    private sealed class MarkerMigration : ISaveMigration
    {
        public int FromVersion => 0;
        public bool Applied { get; private set; }

        public void Apply(JsonNode root)
        {
            root["Marker"] = "migrated";
            Applied = true;
        }
    }
}
