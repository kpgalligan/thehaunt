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

        data.Player.Money = 4321;
        data.Player.Stamina = 37;
        data.Player.MaxStamina = 120;
        data.Player.Inventory.Slots[0] = new ItemStackRecord { ItemId = "turnip", Count = 5 };
        data.Player.Inventory.Slots[1] = new ItemStackRecord { ItemId = "hoe", Count = 1 };
        // Slot 2 deliberately stays null — a hole between stacks must survive.
        data.Player.Inventory.Slots[3] = new ItemStackRecord { ItemId = "greenbean_seeds", Count = 3 };
        data.Player.Inventory.SelectedSlot = 3;
        data.ShippingBin.Add(new ItemStackRecord { ItemId = "greenbean", Count = 4 });
        data.ShippingBin.Add(new ItemStackRecord { ItemId = "turnip", Count = 1 });
        // TotalMinutes 777 is day 0; a known intro flag must be stamped consistently
        // (future-dated known stamps are clamped by load repair — that path has its
        // own test, Save_FutureIntroStampsClamped).
        data.StoryFlags[StoryKeys.FirstPlanting] = 0;

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

            t.AssertEqual(4321L, loaded.Player.Money, "money");
            t.AssertEqual(37, loaded.Player.Stamina, "stamina");
            t.AssertEqual(120, loaded.Player.MaxStamina, "max stamina");
            InventoryData inv = loaded.Player.Inventory;
            AssertStack(t, inv.SlotAt(0), "turnip", 5, "slot 0");
            AssertStack(t, inv.SlotAt(1), "hoe", 1, "slot 1");
            t.Assert(inv.SlotAt(2) == null, "slot 2 null hole survives");
            AssertStack(t, inv.SlotAt(3), "greenbean_seeds", 3, "slot 3");
            t.AssertEqual(3, inv.SelectedSlot, "selected slot");
            t.AssertEqual(2, loaded.ShippingBin.Count, "shipping bin stack count");
            AssertStack(t, loaded.ShippingBin[0], "greenbean", 4, "bin[0]");
            AssertStack(t, loaded.ShippingBin[1], "turnip", 1, "bin[1]");
            t.Assert(loaded.HasFlag(StoryKeys.FirstPlanting), "populated story flag survives");
            t.AssertEqual(0L, loaded.FlagDay(StoryKeys.FirstPlanting), "story flag day-stamp");
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
    public static void Save_FixtureV2Loads(TestContext t)
    {
        string json = ReadFixture(t, "v2_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            // The chain now carries v2 fixtures to the current version; this test's job
            // is the v2 payload surviving, not pinning the chain's endpoint.
            t.AssertEqual(SaveMigrations.CurrentVersion, loaded.SaveVersion, "SaveVersion");
            t.AssertEqual(2400L, loaded.TotalMinutes, "TotalMinutes");
            t.AssertEqual(150f, loaded.Player.X, "player X");
            t.AssertEqual(130f, loaded.Player.Y, "player Y");
            t.AssertEqual(1, loaded.Player.Facing, "player facing");
            t.AssertEqual(750L, loaded.Player.Money, "money");
            t.AssertEqual(40, loaded.Player.Stamina, "stamina");
            t.AssertEqual(100, loaded.Player.MaxStamina, "max stamina");

            InventoryData inv = loaded.Player.Inventory;
            t.AssertEqual(2, inv.SelectedSlot, "selected slot");
            AssertStack(t, inv.SlotAt(0), "hoe", 1, "slot 0");
            t.Assert(inv.SlotAt(1) == null, "slot 1 empty");
            AssertStack(t, inv.SlotAt(2), "turnip_seeds", 7, "slot 2");

            t.AssertEqual(1, loaded.ShippingBin.Count, "bin stack count");
            AssertStack(t, loaded.ShippingBin[0], "turnip", 3, "bin[0]");

            TileRecord? tile = loaded.Maps["test_farm"].GetTile(5, 6);
            t.Assert(tile != null, "tile (5,6) present");
            t.AssertEqual("turnip", tile!.CropId, "tile crop id");
            t.AssertEqual(2, tile.GrowthDay, "tile growth day");
            t.AssertEqual(1L, tile.LastWateredDay, "tile last watered day");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_MigrationV1ToV2(TestContext t)
    {
        string json = ReadFixture(t, "v1_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.AssertEqual(SaveMigrations.CurrentVersion, loaded.SaveVersion,
                "SaveVersion migrated to current");
            t.AssertEqual(500L, loaded.Player.Money, "migrated money grant");
            t.AssertEqual(100, loaded.Player.Stamina, "migrated stamina");
            t.AssertEqual(100, loaded.Player.MaxStamina, "migrated max stamina");

            InventoryData inv = loaded.Player.Inventory;
            t.AssertEqual(0, inv.SelectedSlot, "migrated selected slot");
            AssertStack(t, inv.SlotAt(0), "hoe", 1, "migrated slot 0");
            AssertStack(t, inv.SlotAt(1), "watering_can", 1, "migrated slot 1");
            AssertStack(t, inv.SlotAt(2), "scythe", 1, "migrated slot 2");
            AssertStack(t, inv.SlotAt(3), "turnip_seeds", 15, "migrated slot 3");
            AssertStack(t, inv.SlotAt(4), "greenbean_seeds", 5, "migrated slot 4");
            // Slots 5-6 are the v4→v5 axe/pick grant landing in their preferred
            // starter-kit slots (the chain carries v1 fixtures all the way up).
            AssertStack(t, inv.SlotAt(5), "axe", 1, "migrated slot 5");
            AssertStack(t, inv.SlotAt(6), "pick", 1, "migrated slot 6");
            for (int i = 7; i < InventoryData.Capacity; i++)
            {
                t.Assert(inv.SlotAt(i) == null, $"migrated slot {i} empty");
            }
            t.AssertEqual(0, loaded.ShippingBin.Count, "migrated shipping bin empty");

            // The v1 payload must survive the migration untouched.
            t.AssertEqual(600L, loaded.TotalMinutes, "v1 TotalMinutes survives");
            t.AssertEqual(100f, loaded.Player.X, "v1 player X survives");
            t.AssertEqual(120f, loaded.Player.Y, "v1 player Y survives");
            t.AssertEqual(2, loaded.Player.Facing, "v1 facing survives");
            TileRecord? tile = loaded.Maps["test_farm"].GetTile(3, 4);
            t.Assert(tile != null, "v1 tile (3,4) survives");
            t.AssertEqual("tilled", tile!.Kind, "v1 tile kind survives");
            t.AssertEqual(-1L, tile.LastWateredDay, "v1 tile LastWateredDay survives");

            // Idempotence: a second pass over the migrated data must not re-grant anything.
            string firstPass = SnapshotStacks(loaded);
            service.DeserializeFrom(service.SerializeToString());
            t.AssertEqual(firstPass, SnapshotStacks(service.Current),
                "second load is stack-for-stack identical");
            t.AssertEqual(500L, service.Current.Player.Money, "money not re-granted");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_MigratedKitMatchesNewGame(TestContext t)
    {
        // Drift guard: if the starter kit ever changes, this MUST fail — the fix is a
        // conscious decision (a new migration step), never editing the frozen migrations.
        // The launch-era kit lands in a migrated save's INVENTORY (frozen JSON); a new
        // game stocks the same kit into the barn chest. The two must stay stack-for-stack
        // identical, slot i of one against slot i of the other.
        string json = ReadFixture(t, "v1_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            InventoryData migrated = service.Current.Player.Inventory;
            StorageData fresh = GameData.NewGame().GetStorage(StorageIds.BarnChest);

            t.AssertEqual(0, migrated.SelectedSlot, "selected slot boots at 0 in both");
            for (int i = 0; i < fresh.Slots.Count; i++)
            {
                ItemStackRecord? expected = fresh.Slots[i];
                ItemStackRecord? actual = migrated.SlotAt(i);
                if (expected == null)
                {
                    t.Assert(actual == null, $"slot {i}: empty in both");
                    continue;
                }
                t.Assert(actual != null, $"slot {i}: present in both");
                t.AssertEqual(expected.ItemId, actual!.ItemId, $"slot {i}: item id");
                t.AssertEqual(expected.Count, actual.Count, $"slot {i}: count");
            }
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_UnknownItemIdSurvives(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            var data = new GameData();
            data.Player.Inventory.Slots[4] = new ItemStackRecord { ItemId = "mystery_relic", Count = 3 };
            data.ShippingBin.Add(new ItemStackRecord { ItemId = "mystery_relic", Count = 2 });
            string json = JsonSerializer.Serialize(data, SaveJsonContext.Default.GameData);

            // The load path runs Normalize — the unknown item must survive it intact.
            service.DeserializeFrom(json);
            AssertStack(t, service.Current.Player.Inventory.SlotAt(4),
                "mystery_relic", 3, "unknown item after load");
            t.AssertEqual(1, service.Current.ShippingBin.Count, "bin stack count after load");
            AssertStack(t, service.Current.ShippingBin[0],
                "mystery_relic", 2, "unknown bin item after load");

            service.DeserializeFrom(service.SerializeToString());
            AssertStack(t, service.Current.Player.Inventory.SlotAt(4),
                "mystery_relic", 3, "unknown item after round-trip");
            t.AssertEqual(1, service.Current.ShippingBin.Count, "bin stack count after round-trip");
            AssertStack(t, service.Current.ShippingBin[0],
                "mystery_relic", 2, "unknown bin item after round-trip");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_ShippingBinSanitized(TestContext t)
    {
        // Load repair mirrors Inventory.Normalize: degenerate bin entries (null element,
        // null/empty id, non-positive count) are dropped; unknown-but-well-formed ids are
        // KEPT (item deletion is data loss).
        const string json = """
            {"SaveVersion":2,"TotalMinutes":600,"ShippingBin":[
                null,
                {"ItemId":"turnip","Count":-100},
                {"ItemId":null,"Count":5},
                {"ItemId":"mystery_relic","Count":3},
                {"ItemId":"greenbean","Count":2}]}
            """;
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.AssertEqual(2, loaded.ShippingBin.Count, "exactly the two well-formed stacks survive");
            AssertStack(t, loaded.ShippingBin[0], "mystery_relic", 3, "unknown-but-well-formed id kept");
            AssertStack(t, loaded.ShippingBin[1], "greenbean", 2, "known sellable stack kept");

            // The negative-count turnip stack is gone, so the overnight sale cannot mint
            // negative proceeds: money moves by exactly the greenbean sale (2 x 40).
            // Pre-floor the money so the DevScaffold dawn top-up (step 0, before the
            // sale credit) is a no-op and the delta stays pure.
            loaded.Player.Money = DevScaffold.DailyMoneyFloor;
            long moneyBefore = loaded.Player.Money;
            OvernightSim.Run(loaded, dayEnding: Clock.Instance.Now.DayIndex);
            t.AssertEqual(moneyBefore + 80L, loaded.Player.Money,
                "money increased by exactly 2 x 40 (no negative proceeds)");
            t.AssertEqual(1, loaded.ShippingBin.Count, "unsellable stack still occupies the bin");
            AssertStack(t, loaded.ShippingBin[0], "mystery_relic", 3, "mystery_relic survives the sale");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_ObjectsSanitized(TestContext t)
    {
        // Load repair for MapState.Objects mirrors the shipping bin's: null elements
        // and id-less records are dropped, damage clamps non-negative, a null LIST
        // becomes empty — the obstacle path dereferences this on every boot, so a
        // hand-edited save must die on load repair, never in EnsureObstacles. Unknown
        // ids are KEPT (object deletion is data loss).
        const string json = """
            {"SaveVersion":6,"TotalMinutes":0,
             "Maps":{"test_farm":{"Tiles":[],"Objects":[
                null,
                {"X":1,"Y":1,"ObjectId":null},
                {"X":2,"Y":2,"ObjectId":""},
                {"X":3,"Y":3,"ObjectId":"tree","HitsTaken":-7},
                {"X":4,"Y":4,"ObjectId":"future.shrine","HitsTaken":2}]},
             "town":{"Objects":null}}}
            """;
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            MapState farm = service.Current.GetMap("test_farm");
            t.AssertEqual(2, farm.Objects.Count, "exactly the two well-formed records survive");
            t.AssertEqual("tree", farm.Objects[0].ObjectId, "the tree survives");
            t.AssertEqual(0, farm.Objects[0].HitsTaken, "with its damage clamped to zero");
            t.AssertEqual("future.shrine", farm.Objects[1].ObjectId, "the unknown object is kept");
            t.AssertEqual(2, farm.Objects[1].HitsTaken, "with its damage intact");
            t.AssertEqual(0, service.Current.GetMap("town").Objects.Count,
                "a null Objects list loads as empty, not as a boot crash");
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

    [SimTest]
    public static void Save_MigrationV2ToV3(TestContext t)
    {
        string json = ReadFixture(t, "v2_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            // The chain now carries v2 fixtures past 3; this test's job is the v2
            // payload surviving the v2→v3 step, not pinning the chain's endpoint.
            t.AssertEqual(SaveMigrations.CurrentVersion, loaded.SaveVersion,
                "SaveVersion migrated to current");
            t.Assert(loaded.StoryFlags != null, "StoryFlags present after migration");
            t.AssertEqual(0, loaded.StoryFlags!.Count,
                "migrated StoryFlags empty (the frozen literal adds no flags)");

            // The full v2 payload must survive the migration untouched.
            t.AssertEqual(2400L, loaded.TotalMinutes, "v2 TotalMinutes survives");
            t.AssertEqual(150f, loaded.Player.X, "v2 player X survives");
            t.AssertEqual(130f, loaded.Player.Y, "v2 player Y survives");
            t.AssertEqual(1, loaded.Player.Facing, "v2 facing survives");
            t.AssertEqual(750L, loaded.Player.Money, "v2 money survives");
            t.AssertEqual(40, loaded.Player.Stamina, "v2 stamina survives");
            t.AssertEqual(100, loaded.Player.MaxStamina, "v2 max stamina survives");
            InventoryData inv = loaded.Player.Inventory;
            t.AssertEqual(2, inv.SelectedSlot, "v2 selected slot survives");
            AssertStack(t, inv.SlotAt(0), "hoe", 1, "v2 slot 0");
            t.Assert(inv.SlotAt(1) == null, "v2 slot 1 empty");
            AssertStack(t, inv.SlotAt(2), "turnip_seeds", 7, "v2 slot 2");
            t.AssertEqual(1, loaded.ShippingBin.Count, "v2 bin stack count survives");
            AssertStack(t, loaded.ShippingBin[0], "turnip", 3, "v2 bin[0]");
            TileRecord? tile = loaded.Maps["test_farm"].GetTile(5, 6);
            t.Assert(tile != null, "v2 tile (5,6) survives");
            t.AssertEqual("turnip", tile!.CropId, "v2 tile crop survives");
            t.AssertEqual(2, tile.GrowthDay, "v2 tile growth day survives");
            t.AssertEqual(1L, tile.LastWateredDay, "v2 tile last watered day survives");

            // Idempotence: re-serializing and re-loading the migrated data must be
            // byte-identical — the only-if-absent guard makes a second pass a no-op.
            string firstPass = service.SerializeToString();
            service.DeserializeFrom(firstPass);
            t.AssertEqual(firstPass, service.SerializeToString(),
                "second migration pass serializes byte-identically");
            t.AssertEqual(0, service.Current.StoryFlags.Count,
                "StoryFlags still empty on the second pass");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_MigrationChainV1ToV3(TestContext t)
    {
        string json = ReadFixture(t, "v1_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.AssertEqual(SaveMigrations.CurrentVersion, loaded.SaveVersion,
                "SaveVersion migrated from 1 to current through the chain");

            // The frozen v1 -> v2 step: starter kit + money grant.
            t.AssertEqual(500L, loaded.Player.Money, "chained money grant");
            t.AssertEqual(100, loaded.Player.Stamina, "chained stamina");
            t.AssertEqual(100, loaded.Player.MaxStamina, "chained max stamina");
            InventoryData inv = loaded.Player.Inventory;
            AssertStack(t, inv.SlotAt(0), "hoe", 1, "chained slot 0");
            AssertStack(t, inv.SlotAt(1), "watering_can", 1, "chained slot 1");
            AssertStack(t, inv.SlotAt(2), "scythe", 1, "chained slot 2");
            AssertStack(t, inv.SlotAt(3), "turnip_seeds", 15, "chained slot 3");
            AssertStack(t, inv.SlotAt(4), "greenbean_seeds", 5, "chained slot 4");

            // The frozen v2 -> v3 step: empty flags.
            t.Assert(loaded.StoryFlags != null, "StoryFlags present after the chain");
            t.AssertEqual(0, loaded.StoryFlags!.Count, "chained StoryFlags empty");

            // The v1 payload must survive both steps untouched.
            t.AssertEqual(600L, loaded.TotalMinutes, "v1 TotalMinutes survives the chain");
            t.AssertEqual(100f, loaded.Player.X, "v1 player X survives the chain");
            t.AssertEqual(120f, loaded.Player.Y, "v1 player Y survives the chain");
            t.AssertEqual(2, loaded.Player.Facing, "v1 facing survives the chain");
            TileRecord? tile = loaded.Maps["test_farm"].GetTile(3, 4);
            t.Assert(tile != null, "v1 tile (3,4) survives the chain");
            t.AssertEqual("tilled", tile!.Kind, "v1 tile kind survives the chain");
            t.AssertEqual(-1L, tile.LastWateredDay, "v1 tile LastWateredDay survives the chain");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_StoryFlagsRoundTrip(TestContext t)
    {
        // Flag repair mirrors the shipping-bin repair: the one degenerate key ("") is
        // dropped, negative stamps clamp to 0, and unknown-but-well-formed flag ids are
        // KEPT (the preserve-unknown-ids rule applied to flags).
        const string json = """
            {"SaveVersion":3,"TotalMinutes":0,"StoryFlags":{
                "":5,
                "intro.first_planting":-4,
                "future.mystery_flag":9}}
            """;
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.Assert(!loaded.HasFlag(""), "empty-string key dropped by load repair");
            t.AssertEqual(0L, loaded.FlagDay(StoryKeys.FirstPlanting), "negative stamp clamped to 0");
            t.AssertEqual(9L, loaded.FlagDay("future.mystery_flag"), "unknown flag kept with its stamp");
            t.AssertEqual(2, loaded.StoryFlags.Count, "exactly the two repaired flags remain");

            // The unknown key must re-serialize byte-exactly and survive a full round-trip.
            string reserialized = service.SerializeToString();
            t.Assert(reserialized.Contains("\"future.mystery_flag\": 9"),
                "unknown flag re-serializes byte-exactly");   // WriteIndented puts a space after ':'
            service.DeserializeFrom(reserialized);
            t.AssertEqual(9L, service.Current.FlagDay("future.mystery_flag"),
                "unknown flag survives the round-trip");
            t.AssertEqual(0L, service.Current.FlagDay(StoryKeys.FirstPlanting),
                "clamped stamp survives the round-trip");
            t.AssertEqual(2, service.Current.StoryFlags.Count,
                "flag count stable across the round-trip");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_FutureIntroStampsClamped(TestContext t)
    {
        // A future-dated stamp on a KNOWN intro flag would wedge the day-comparison
        // rules forever (road never clears; only-if-absent flags cannot be restamped).
        // Load repair clamps known intro stamps to the save's own day; unknown flags
        // keep their stamps verbatim (preserve-unknown rule).
        const string json = """
            {"SaveVersion":3,"TotalMinutes":3600,"StoryFlags":{
                "intro.first_planting":999999,
                "future.mystery_flag":999999}}
            """;
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            long saveDay = new GameTime(3600).DayIndex;
            t.AssertEqual(saveDay, service.Current.FlagDay(StoryKeys.FirstPlanting),
                "future-dated known stamp clamped to the save's day");
            t.AssertEqual(999999L, service.Current.FlagDay("future.mystery_flag"),
                "unknown flag stamp preserved verbatim");
            // Liveness: the very next dawn clears the road again.
            var flags = IntroRules.FlagsToSetOnDayStarted(service.Current, saveDay + 1);
            t.Assert(flags.Contains(StoryKeys.RoadCleared),
                "clamped stamp restores intro progress at the next dawn");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_FixtureV3Loads(TestContext t)
    {
        // The frozen v3 fixture is the v4 migration's input: a meeting-pending
        // mid-story state (player in town, crew arrival done) with an unknown flag.
        string json = ReadFixture(t, "v3_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.AssertEqual(SaveMigrations.CurrentVersion, loaded.SaveVersion, "SaveVersion");
            t.AssertEqual(3600L, loaded.TotalMinutes, "TotalMinutes");
            t.AssertEqual("town", loaded.Player.MapId, "player mid-story in town");
            t.AssertEqual(760L, loaded.Player.Money, "money");
            t.AssertEqual(80, loaded.Player.Stamina, "stamina");
            t.AssertEqual(1L, loaded.FlagDay(StoryKeys.FirstPlanting), "first_planting stamp");
            t.AssertEqual(2L, loaded.FlagDay(StoryKeys.RoadCleared), "road_cleared stamp");
            t.AssertEqual(2L, loaded.FlagDay(StoryKeys.CrewArrivalDone), "crew_arrival_done stamp");
            t.Assert(!loaded.HasFlag(StoryKeys.MeetingDone), "meeting_done absent");
            t.AssertEqual(9L, loaded.FlagDay("future.mystery_flag"), "unknown flag preserved");

            // Meeting pending per PendingBeat: nothing fires where the player stands
            // (town, 6:00 AM), but the state derives the meeting at the hall in-window.
            var now = new GameTime(loaded.TotalMinutes);
            t.Assert(IntroRules.PendingBeat(loaded, now, loaded.Player.MapId) == null,
                "nothing pends in town at 6:00 AM");
            var evening = new GameTime(
                now.DayIndex * GameTime.MinutesPerDay + IntroRules.MeetingStartMinuteOfDay);
            t.AssertEqual((StoryBeatId?)StoryBeatId.TownMeeting,
                IntroRules.PendingBeat(loaded, evening, MapIds.TownHall),
                "meeting pends at the hall from 18:00");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_MigratedStoryMatchesNewGame(TestContext t)
    {
        // Drift guard: if NewGame ever gains starting flags, this MUST fail — the fix is
        // a conscious decision (a new migration step), never editing the frozen v2→v3
        // migration. Zero flags IS the "morning after the storm" state.
        string json = ReadFixture(t, "v2_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            Dictionary<string, long> migrated = service.Current.StoryFlags;
            Dictionary<string, long> fresh = GameData.NewGame().StoryFlags;

            t.AssertEqual(fresh.Count, migrated.Count, "flag count matches new game");
            foreach ((string key, long day) in fresh)
            {
                t.Assert(migrated.TryGetValue(key, out long migratedDay) && migratedDay == day,
                    $"flag '{key}' matches new game");
            }
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_MigrationV3ToV4(TestContext t)
    {
        string json = ReadFixture(t, "v3_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.AssertEqual(SaveMigrations.CurrentVersion, loaded.SaveVersion,
                "SaveVersion migrated to current");
            t.Assert(loaded.Storages != null, "Storages present after migration");
            t.AssertEqual(0, loaded.Storages!.Count,
                "migrated Storages empty (the frozen literal adds no storages)");

            // The full v3 payload must survive the migration untouched.
            t.AssertEqual(3600L, loaded.TotalMinutes, "v3 TotalMinutes survives");
            t.AssertEqual("town", loaded.Player.MapId, "v3 player MapId survives");
            t.AssertEqual(56f, loaded.Player.X, "v3 player X survives");
            t.AssertEqual(232f, loaded.Player.Y, "v3 player Y survives");
            t.AssertEqual(2, loaded.Player.Facing, "v3 facing survives");
            t.AssertEqual(760L, loaded.Player.Money, "v3 money survives");
            t.AssertEqual(80, loaded.Player.Stamina, "v3 stamina survives");
            t.AssertEqual(100, loaded.Player.MaxStamina, "v3 max stamina survives");
            AssertStack(t, loaded.Player.Inventory.SlotAt(0), "hoe", 1, "v3 slot 0");
            TileRecord? tile = loaded.Maps["test_farm"].GetTile(5, 6);
            t.Assert(tile != null, "v3 tile (5,6) survives");
            t.AssertEqual("turnip", tile!.CropId, "v3 tile crop survives");
            t.AssertEqual(3, tile.GrowthDay, "v3 tile growth day survives");
            t.AssertEqual(2L, tile.LastWateredDay, "v3 tile last watered day survives");
            t.AssertEqual(1L, loaded.FlagDay(StoryKeys.FirstPlanting), "v3 first_planting survives");
            t.AssertEqual(2L, loaded.FlagDay(StoryKeys.RoadCleared), "v3 road_cleared survives");
            t.AssertEqual(2L, loaded.FlagDay(StoryKeys.CrewArrivalDone), "v3 crew_arrival_done survives");
            t.AssertEqual(9L, loaded.FlagDay("future.mystery_flag"), "v3 unknown flag survives");

            // Idempotence: re-serializing and re-loading the migrated data must be
            // byte-identical — the only-if-absent guard makes a second pass a no-op.
            string firstPass = service.SerializeToString();
            service.DeserializeFrom(firstPass);
            t.AssertEqual(firstPass, service.SerializeToString(),
                "second migration pass serializes byte-identically");
            t.AssertEqual(0, service.Current.Storages.Count,
                "Storages still empty on the second pass");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_MigrationV5ToV6(TestContext t)
    {
        // v6 is a pure version-gate bump for the field obstacles: nothing rewritten,
        // absent ObstaclesSeeded reads false (the farm generates on next visit) and
        // absent HitsTaken reads 0. The proof is defaults plus byte-identity.
        string json = ReadFixture(t, "v5_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            t.AssertEqual(SaveMigrations.CurrentVersion, service.Current.SaveVersion, "version bumped");
            MapState farm = service.Current.GetMap("test_farm");
            t.Assert(!farm.ObstaclesSeeded, "a v5 farm has never been seeded");
            t.AssertEqual(1, farm.Objects.Count, "the v5 object survives");
            t.AssertEqual("future.relic", farm.Objects[0].ObjectId, "with its unknown id preserved");
            t.AssertEqual(0, farm.Objects[0].HitsTaken, "and undamaged");
            t.Assert(farm.GetTile(5, 5)?.Kind == "tilled", "the tilled plot survives");

            string firstPass = service.SerializeToString();
            service.DeserializeFrom(firstPass);
            t.AssertEqual(firstPass, service.SerializeToString(),
                "second migration pass serializes byte-identically");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_MigrationV4ToV5(TestContext t)
    {
        string json = ReadFixture(t, "v4_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            InventoryData inv = service.Current.Player.Inventory;
            // The grant lands in the tools' preferred starter-kit slots when free.
            AssertStack(t, inv.SlotAt(0), "hoe", 1, "v4 slot 0 survives");
            AssertStack(t, inv.SlotAt(5), "axe", 1, "axe granted at slot 5");
            AssertStack(t, inv.SlotAt(6), "pick", 1, "pick granted at slot 6");

            // Idempotence: re-serializing and re-loading the migrated data must be
            // byte-identical — the only-if-absent guard makes a second pass a no-op.
            string firstPass = service.SerializeToString();
            service.DeserializeFrom(firstPass);
            t.AssertEqual(firstPass, service.SerializeToString(),
                "second migration pass serializes byte-identically");

            // Occupied preferred slot: the grant falls back to the FIRST free slot
            // (not fill-forward past it), and an owned tool is never duplicated.
            const string crowded = """
                {"SaveVersion":4,"TotalMinutes":0,"Player":{"Inventory":{"Slots":[
                    {"ItemId":"axe","Count":1},{"ItemId":"turnip","Count":3},null,
                    {"ItemId":"hoe","Count":1},null,{"ItemId":"scythe","Count":1},
                    {"ItemId":"turnip_seeds","Count":4},null,null,null],
                    "SelectedSlot":0}}}
                """;
            service.DeserializeFrom(crowded);
            inv = service.Current.Player.Inventory;
            t.AssertEqual(1, inv.CountOf("axe"), "owned axe not duplicated");
            AssertStack(t, inv.SlotAt(2), "pick", 1, "pick fell back to first free slot");
            t.Assert(inv.SlotAt(4) == null, "later holes untouched");

            // Full inventory: the grant is skipped outright, nothing is displaced.
            const string full = """
                {"SaveVersion":4,"TotalMinutes":0,"Player":{"Inventory":{"Slots":[
                    {"ItemId":"turnip","Count":1},{"ItemId":"turnip","Count":1},
                    {"ItemId":"turnip","Count":1},{"ItemId":"turnip","Count":1},
                    {"ItemId":"turnip","Count":1},{"ItemId":"turnip","Count":1},
                    {"ItemId":"turnip","Count":1},{"ItemId":"turnip","Count":1},
                    {"ItemId":"turnip","Count":1},{"ItemId":"turnip","Count":1}],
                    "SelectedSlot":0}}}
                """;
            service.DeserializeFrom(full);
            inv = service.Current.Player.Inventory;
            t.AssertEqual(0, inv.CountOf("axe"), "full inventory: axe grant skipped");
            t.AssertEqual(0, inv.CountOf("pick"), "full inventory: pick grant skipped");
            t.AssertEqual(10, inv.CountOf("turnip"), "full inventory: nothing displaced");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_MigratedStorageMatchesNewGame(TestContext t)
    {
        // Drift guard: a migrated save's Storages stay EMPTY — the frozen migrations
        // grant the launch kit straight to the player's inventory, so stocking the barn
        // chest too would double-grant it; old saves' chests materialize on first open.
        // A new game ships exactly one pre-filled storage, the barn chest (StarterKit).
        // If NewGame ever gains another, this MUST fail — the fix is a conscious
        // decision (a new migration step), never editing the frozen v3→v4 migration.
        string json = ReadFixture(t, "v3_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            Dictionary<string, StorageData> migrated = service.Current.Storages;
            Dictionary<string, StorageData> fresh = GameData.NewGame().Storages;

            t.AssertEqual(0, migrated.Count, "migrated Storages empty (kit already in inventory)");
            t.AssertEqual(1, fresh.Count, "new game ships only the stocked barn chest");
            t.Assert(fresh.ContainsKey(StorageIds.BarnChest), "and that storage is the barn chest");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_StorageRoundTrip(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            var data = new GameData();
            StorageData chest = data.GetStorage(StorageIds.FarmHouseChest);
            chest.Slots[0] = new ItemStackRecord { ItemId = "turnip", Count = 3 };
            // Slot 5 leaves a null hole below it — index stability must survive.
            chest.Slots[5] = new ItemStackRecord { ItemId = "mystery_relic", Count = 2 };
            data.Storages["future.locker"] = new StorageData
            {
                Slots = { new ItemStackRecord { ItemId = "turnip_seeds", Count = 1 } },
            };
            string json = JsonSerializer.Serialize(data, SaveJsonContext.Default.GameData);

            // The load path runs the storage repair — everything must survive it intact.
            service.DeserializeFrom(json);
            AssertStorageRoundTripStacks(t, service.Current, "after load");

            service.DeserializeFrom(service.SerializeToString());
            AssertStorageRoundTripStacks(t, service.Current, "after round-trip");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_FixtureV4Loads(TestContext t)
    {
        // The frozen v4 fixture pins storage preservation: a known chest (padded on
        // load) holding an unknown item id, plus an unknown storage KEY that must
        // round-trip verbatim and un-padded.
        string json = ReadFixture(t, "v4_minimal.json");
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.AssertEqual(SaveMigrations.CurrentVersion, loaded.SaveVersion, "SaveVersion");
            t.AssertEqual(480L, loaded.TotalMinutes, "TotalMinutes");
            t.AssertEqual("test_farm", loaded.Player.MapId, "player map");
            t.AssertEqual(100L, loaded.Player.Money, "money");
            AssertV4Storages(t, loaded, "after load");

            service.DeserializeFrom(service.SerializeToString());
            AssertV4Storages(t, service.Current, "after round-trip");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void Save_StorageRepairRules(TestContext t)
    {
        // Storage repair mirrors the flag/bin repair: the one degenerate key ("") is
        // dropped, null values revive as empty storages, degenerate entries are
        // nulled in place, and known storage ids pad to capacity.
        const string json = """
            {"SaveVersion":4,"TotalMinutes":0,"Storages":{
                "":{"Slots":[{"ItemId":"turnip","Count":1}]},
                "farm_house_chest":{"Slots":[
                    {"ItemId":"","Count":5},
                    {"ItemId":"turnip","Count":-2},
                    {"ItemId":"turnip","Count":3}]},
                "future.locker":null}}
            """;
        SaveService service = SaveService.Instance;
        try
        {
            service.DeserializeFrom(json);
            GameData loaded = service.Current;
            t.Assert(!loaded.Storages.ContainsKey(""), "empty-string key dropped by load repair");
            t.AssertEqual(2, loaded.Storages.Count, "exactly the two repaired storages remain");

            StorageData chest = loaded.Storages[StorageIds.FarmHouseChest];
            t.AssertEqual(20, chest.Slots.Count, "known chest padded to 20 slots");
            t.Assert(chest.Slots[0] == null, "empty-id entry nulled");
            t.Assert(chest.Slots[1] == null, "non-positive-count entry nulled");
            AssertStack(t, chest.Slots[2], "turnip", 3, "well-formed stack kept in place");

            t.Assert(loaded.Storages["future.locker"] != null,
                "null storage value repaired to an empty storage");
            t.AssertEqual(0, loaded.Storages["future.locker"].Slots.Count,
                "unknown storage key NOT padded (its capacity is not ours to invent)");

            // Over-capacity chest: 25 slots survive un-trimmed with stacks in place
            // (raising a capacity later must stay a constant change, not a migration).
            var over = new GameData();
            var big = new StorageData();
            for (int i = 0; i < 24; i++)
            {
                big.Slots.Add(null);
            }
            big.Slots.Add(new ItemStackRecord { ItemId = "turnip", Count = 1 });
            over.Storages[StorageIds.FarmHouseChest] = big;
            service.DeserializeFrom(JsonSerializer.Serialize(over, SaveJsonContext.Default.GameData));
            StorageData kept = service.Current.Storages[StorageIds.FarmHouseChest];
            t.AssertEqual(25, kept.Slots.Count, "over-capacity chest NOT trimmed");
            AssertStack(t, kept.Slots[24], "turnip", 1, "over-capacity stack kept in place");
        }
        finally
        {
            service.NewGame();
        }
    }

    private static void AssertStorageRoundTripStacks(TestContext t, GameData data, string label)
    {
        t.Assert(data.Storages.ContainsKey(StorageIds.FarmHouseChest), $"{label}: chest present");
        StorageData chest = data.Storages[StorageIds.FarmHouseChest];
        t.AssertEqual(20, chest.Slots.Count, $"{label}: chest still at capacity");
        AssertStack(t, chest.Slots[0], "turnip", 3, $"{label}: chest slot 0");
        t.Assert(chest.Slots[1] == null, $"{label}: chest null hole survives");
        AssertStack(t, chest.Slots[5], "mystery_relic", 2, $"{label}: unknown item id kept at slot 5");

        t.Assert(data.Storages.ContainsKey("future.locker"), $"{label}: unknown storage key kept");
        StorageData locker = data.Storages["future.locker"];
        t.AssertEqual(1, locker.Slots.Count, $"{label}: unknown storage NOT padded");
        AssertStack(t, locker.Slots[0], "turnip_seeds", 1, $"{label}: locker slot 0");
    }

    private static void AssertV4Storages(TestContext t, GameData data, string label)
    {
        t.AssertEqual(2, data.Storages.Count, $"{label}: exactly the two fixture storages");
        StorageData chest = data.Storages[StorageIds.FarmHouseChest];
        t.AssertEqual(20, chest.Slots.Count, $"{label}: chest normalized (padded) to 20 slots");
        AssertStack(t, chest.Slots[0], "turnip", 3, $"{label}: chest slot 0");
        AssertStack(t, chest.Slots[1], "future.artifact", 2,
            $"{label}: chest slot 1 (unknown item preserved)");
        for (int i = 2; i < chest.Slots.Count; i++)
        {
            t.Assert(chest.Slots[i] == null, $"{label}: chest slot {i} empty");
        }

        StorageData locker = data.Storages["future.locker"];
        t.AssertEqual(1, locker.Slots.Count, $"{label}: unknown storage preserved un-padded");
        AssertStack(t, locker.Slots[0], "turnip_seeds", 1, $"{label}: locker slot 0");
    }

    private static string ReadFixture(TestContext t, string name)
    {
        using var file = Godot.FileAccess.Open(
            $"res://src/Tests/fixtures/{name}", Godot.FileAccess.ModeFlags.Read);
        t.Assert(file != null, $"fixture '{name}' opens: {Godot.FileAccess.GetOpenError()}");
        return file!.GetAsText();
    }

    private static void AssertStack(TestContext t, ItemStackRecord? stack, string itemId, int count, string label)
    {
        t.Assert(stack != null, $"{label}: stack present");
        t.AssertEqual(itemId, stack!.ItemId, $"{label}: item id");
        t.AssertEqual(count, stack.Count, $"{label}: count");
    }

    // One-line encoding of every stack (inventory slots, selection, shipping bin) so two
    // save states can be compared stack-for-stack with a single string equality.
    private static string SnapshotStacks(GameData data)
    {
        var parts = new List<string>();
        foreach (ItemStackRecord? stack in data.Player.Inventory.Slots)
        {
            parts.Add(stack == null ? "-" : $"{stack.ItemId}x{stack.Count}");
        }
        parts.Add($"sel:{data.Player.Inventory.SelectedSlot}");
        foreach (ItemStackRecord stack in data.ShippingBin)
        {
            parts.Add($"bin:{stack.ItemId}x{stack.Count}");
        }
        return string.Join("|", parts);
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
