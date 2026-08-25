using System.Text.Json.Nodes;
using Godot;
using TheHaunt.Core;
using TheHaunt.Player;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class IntegrationTests
{
    // Bed Area2D position in TestMap: footprint center of tiles (8,8)-(8,9). See spec §3.
    private static readonly Vector2 BedPosition = new(136, 152);

    [SimTest]
    public static async Task Events_MapSwapStress(TestContext t)
    {
        // Catches leaked C# event subscriptions and stale WorldSim map registrations on
        // freed nodes: any handler still wired to the clock, or a freed map still resolved
        // by tool use or the overnight repaint, must crash a later cycle.
        try
        {
            SaveService.Instance.NewGame(); // clock -> 0, starter kit, MapId "test_farm"
            // Crew staging pre-stamped once: with the road cleared and the arrival beat
            // pending, every cycle's NPC sync must spawn crew views on the fresh map — a
            // leaked view or a stale registration on a freed map must crash a later
            // cycle. No Main is booted, so no beats fire.
            WorldSim.Instance.SetStoryFlag(StoryKeys.FirstPlanting);
            WorldSim.Instance.SetStoryFlag(StoryKeys.RoadCleared);
            for (int i = 0; i < 50; i++)
            {
                var map = new TestMap { MapId = "test_farm" };
                t.Host.AddChild(map);
                await t.WaitFrames(1);
                WorldSim.Instance.SyncNpcsNow(); // between instance and free: stale references crash here
                Clock.Instance.AdvanceMinutes(10);
                WorldSim.Instance.SelectSlot(0); // hoe
                // Vary the target so the RefreshTile path runs on a fresh tile each of the
                // first 30 cycles. Row 27 is plain walkable grass across x 3..36 — inside
                // the water border, no stones, no dirt patch, no interactables.
                var tile = new Vector2I(3 + (i % 30), 27);
                ActionOutcome outcome = WorldSim.Instance.UseSelectedItem(tile);
                if (i < 30)
                {
                    t.AssertEqual(ActionOutcome.Tilled, outcome, $"cycle {i}: fresh tile tilled");
                }
                Clock.Instance.AdvanceToDayStart();
                map.Free();
                await t.WaitFrames(1);
            }
            t.AssertEqual(50L, Clock.Instance.Now.DayIndex,
                "clock advanced one day per cycle through all 50 swaps");
        }
        finally
        {
            SaveService.Instance.NewGame();
        }
    }

    [SimTest]
    public static async Task Farm_ReservedTilesRefuseTilling(TestContext t)
    {
        // Interactable footprint tiles sit on tillable terrain but are reserved: tilling
        // under a bed/sign/bin would render invisibly beneath its sprite. See spec §3.
        SaveService service = SaveService.Instance;
        TestMap? map = null;
        try
        {
            service.NewGame(); // clock -> day 0, starter kit
            service.Current.Player.MapId = "test_farm";
            map = new TestMap { MapId = "test_farm" };
            t.Host.AddChild(map);
            await t.WaitFrames(1);

            WorldSim.Instance.SelectSlot(0); // hoe
            var binTile = new Vector2I(10, 8); // shipping-bin footprint
            t.AssertEqual(ActionOutcome.InvalidTarget, WorldSim.Instance.UseSelectedItem(binTile),
                "hoe on the shipping-bin tile refused as InvalidTarget");
            t.Assert(service.Current.GetMap("test_farm").GetTile(binTile.X, binTile.Y) == null,
                "no TileRecord created at the shipping-bin tile");

            t.Assert(!map.IsTillable(8, 8), "bed tile (8,8) not tillable");
            t.Assert(!map.IsTillable(8, 9), "bed tile (8,9) not tillable");
            t.Assert(!map.IsTillable(12, 8), "sign tile (12,8) not tillable");
            t.Assert(!map.IsTillable(10, 8), "shipping-bin tile (10,8) not tillable");
            t.Assert(map.IsTillable(20, 25), "clear grass tile (20,25) stays tillable");
        }
        finally
        {
            if (map != null && GodotObject.IsInstanceValid(map))
            {
                map.Free();
            }
            await t.WaitFrames(1);
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static async Task Integration_MainBootAndSleep(TestContext t)
    {
        Node? main = null;
        try
        {
            var packed = GD.Load<PackedScene>("res://scenes/Main.tscn");
            main = packed.Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);

            t.Assert(main.GetNodeOrNull<Node2D>("World/Player") != null, "World/Player exists after boot");

            long dayBefore = Clock.Instance.Now.DayIndex;
            GameState.Instance.TransitionTo(GameState.Phase.Sleeping);

            bool completed = await t.WaitUntil(
                () => Clock.Instance.Now.DayIndex > dayBefore
                    && GameState.Instance.Current == GameState.Phase.Playing,
                10);
            t.Assert(completed, "sleep flow advanced the day and returned to Playing within 10 s");
            t.Assert(SaveService.Instance.SaveFileExists(),
                $"autosave file exists for slot '{SaveService.DefaultSlot}'");

            // Prove the autosave round-trips from disk: reboot Main and let its boot
            // path Load the file — the loaded clock must match the post-sleep morning.
            long expectedMinutes = Clock.Instance.Now.TotalMinutes;
            main.Free();
            main = null;
            await t.WaitFrames(1);
            SaveService.Instance.NewGame(); // reset, so the reboot's Load visibly changes state
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(expectedMinutes, Clock.Instance.Now.TotalMinutes,
                "rebooted Main loaded the autosave from disk");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Interaction_ProbeFindsBed(TestContext t)
    {
        Node? main = null;
        try
        {
            var packed = GD.Load<PackedScene>("res://scenes/Main.tscn");
            main = packed.Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);

            // Guards against a leaked autosave from an earlier test coupling this boot.
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            var maybePlayer = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(maybePlayer != null, "World/Player exists after boot");
            PlayerController player = maybePlayer!;

            // Stand just below the bed and face up so the probe reaches into it.
            player.GlobalPosition = BedPosition + new Vector2(0, 28);
            player.Probe.SetFacing(3);

            bool focused = await t.WaitUntil(() => player.Probe.Focused != null, 2);
            t.Assert(focused, "probe focused an interactable near the bed within 2 s");
            t.AssertEqual("Sleep", player.Probe.Focused!.PromptText, "focused prompt text");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Visual_RebuildEqualsIncremental(TestContext t)
    {
        SaveService service = SaveService.Instance;
        TestMap? map = null;
        try
        {
            service.NewGame(); // clock -> day 0, starter kit
            service.Current.Player.MapId = "test_farm";
            map = new TestMap { MapId = "test_farm" };
            t.Host.AddChild(map);
            await t.WaitFrames(1);

            var tile = new Vector2I(20, 14); // dirt rectangle, obstacle-free

            WorldSim.Instance.SelectSlot(0); // hoe
            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(tile), "till outcome");
            AssertCells(t, map, tile, "after till");

            WorldSim.Instance.SelectSlot(3); // turnip seeds
            t.AssertEqual(ActionOutcome.Planted, WorldSim.Instance.UseSelectedItem(tile), "plant outcome");
            AssertCells(t, map, tile, "after plant");

            WorldSim.Instance.SelectSlot(1); // watering can
            t.AssertEqual(ActionOutcome.Watered, WorldSim.Instance.UseSelectedItem(tile), "water outcome");
            AssertCells(t, map, tile, "after water");

            Vector2I soilIncremental = CellOf(t, map, "FarmSoil", tile, "pre-rebuild");
            Vector2I cropIncremental = CellOf(t, map, "Crops", tile, "pre-rebuild");
            t.Assert(soilIncremental != EmptyCell, "watered soil cell painted");
            t.Assert(cropIncremental != EmptyCell, "planted crop cell painted");

            // Full rebuild from the model must reproduce the incremental cells exactly.
            map.Free();
            map = null;
            await t.WaitFrames(1);
            map = new TestMap { MapId = "test_farm" };
            t.Host.AddChild(map);
            await t.WaitFrames(1);
            map.ApplyState(service.Current.GetMap("test_farm"));
            t.AssertEqual(soilIncremental, CellOf(t, map, "FarmSoil", tile, "rebuild"),
                "rebuilt FarmSoil equals incremental");
            t.AssertEqual(cropIncremental, CellOf(t, map, "Crops", tile, "rebuild"),
                "rebuilt Crops equals incremental");
            AssertCells(t, map, tile, "after rebuild");

            // Sleep: growth applies and the overnight repaint flips wet soil to dry.
            Clock.Instance.AdvanceToDayStart();
            TileRecord record = service.Current.GetMap("test_farm").GetTile(tile.X, tile.Y)!;
            t.AssertEqual(1, record.GrowthDay, "crop grew overnight");
            t.AssertEqual(SoilDry, CellOf(t, map, "FarmSoil", tile, "post-sleep"),
                "wet flipped to dry after sleep");
            t.AssertEqual(ExpectedCropCell(record), CellOf(t, map, "Crops", tile, "post-sleep"),
                "stage advanced visually after sleep");
            AssertCells(t, map, tile, "after sleep");

            // Implementation-independent pin (deliberately NOT computed via StageForDay):
            // GrowthDay 1 -> stage column 1; turnip is row 0 (first in CropDefs order).
            t.AssertEqual(new Vector2I(1, 0), CellOf(t, map, "Crops", tile, "post-sleep pin"),
                "post-sleep turnip Crops cell pinned to hardcoded (1, 0)");
        }
        finally
        {
            if (map != null && GodotObject.IsInstanceValid(map))
            {
                map.Free();
            }
            await t.WaitFrames(1);
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static async Task Integration_FullFarmLoop(TestContext t)
    {
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);

            // Guards against a leaked autosave from an earlier test coupling this boot.
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");
            var maybePlayer = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(maybePlayer != null, "World/Player exists after boot");

            // intro beats disabled: this test exercises the farm loop, not the story
            // (without this, the crew beat fires the morning after the first planting
            // and SleepOneNight's WaitUntil(Playing) hangs — standing rule for every
            // Main-booting test that plants and sleeps).
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            // Stand on the dirt patch so the farming happens under the player's feet.
            var tile = new Vector2I(20, 14);
            maybePlayer!.GlobalPosition = new Vector2(tile.X * 16 + 8, tile.Y * 16 + 8);

            WorldSim.Instance.SelectSlot(0); // hoe
            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(tile), "till");
            WorldSim.Instance.SelectSlot(3); // turnip seeds
            t.AssertEqual(ActionOutcome.Planted, WorldSim.Instance.UseSelectedItem(tile), "plant");

            CropDef turnip = CropDefs.Get("turnip");
            for (int night = 0; night < turnip.TotalDays; night++)
            {
                WorldSim.Instance.SelectSlot(1); // watering can
                t.AssertEqual(ActionOutcome.Watered, WorldSim.Instance.UseSelectedItem(tile),
                    $"water before night {night}");
                await SleepOneNight(t, $"night {night}");
            }
            TileRecord record = SaveService.Instance.Current.GetMap("test_farm").GetTile(tile.X, tile.Y)!;
            t.AssertEqual(turnip.TotalDays, record.GrowthDay, "crop mature after watered nights");

            // The road still cleared mid-test (planting day 0 => dawn 1) — harmlessly:
            // the pre-stamped completion flags kept every morning beat-free, so the
            // sleep loop above never stalled outside Playing.
            t.Assert(SaveService.Instance.Current.HasFlag(StoryKeys.RoadCleared),
                "road cleared mid-test without disturbing the farm loop");
            t.Assert(WorldSim.Instance.ActiveDialogue == null, "no beat dialogue ever started");

            InventoryData inv = SaveService.Instance.Current.Player.Inventory;
            int turnipsBefore = inv.CountOf("turnip");
            t.AssertEqual(ActionOutcome.Harvested, WorldSim.Instance.UseSelectedItem(tile), "harvest");
            t.AssertEqual(turnipsBefore + turnip.HarvestCount, inv.CountOf("turnip"), "harvest yield");

            int turnipSlot = -1;
            for (int i = 0; i < InventoryData.Capacity; i++)
            {
                if (inv.SlotAt(i)?.ItemId == "turnip")
                {
                    turnipSlot = i;
                    break;
                }
            }
            t.Assert(turnipSlot >= 0, "harvested turnip landed in a slot");
            WorldSim.Instance.SelectSlot(turnipSlot);
            int shipped = inv.SlotAt(turnipSlot)!.Count;
            t.Assert(WorldSim.Instance.DepositSelectedToBin(), "deposit to shipping bin");

            long moneyBefore = SaveService.Instance.Current.Player.Money;
            long expectedProceeds = (long)ItemDefs.Get("turnip").SellPrice * shipped;
            await SleepOneNight(t, "shipping night");
            t.AssertEqual(moneyBefore + expectedProceeds, SaveService.Instance.Current.Player.Money,
                "money increased by the exact sale sum");
            t.AssertEqual(0, SaveService.Instance.Current.ShippingBin.Count, "bin emptied overnight");
            t.Assert(SaveService.Instance.SaveFileExists(),
                $"autosave file exists for slot '{SaveService.DefaultSlot}'");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Integration_FullIntro(TestContext t)
    {
        // Headless capstone: the game's whole opening, end to end, against the real
        // Main + StoryDirector — blocked road, first planting, crew beat, town travel,
        // evening meeting, and a reload that re-derives a quiet, completed intro.
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");
            t.Assert(!SaveService.Instance.Current.HasFlag(StoryKeys.RoadCleared),
                "fresh game: road not cleared");
            AssertBlockade(t, main, present: true, "boot");

            // Sleeping WITHOUT planting leaves the road blocked — no timer clears it.
            await SleepOneNight(t, "unplanted night");
            t.AssertEqual(1L, Clock.Instance.Now.DayIndex, "morning 2 reached");
            t.Assert(!SaveService.Instance.Current.HasFlag(StoryKeys.RoadCleared),
                "morning 2: road still blocked without planting");
            AssertBlockade(t, main, present: true, "morning 2");

            // First planting (day 1) via the bus.
            var tile = new Vector2I(20, 14); // dirt rectangle, obstacle-free
            WorldSim.Instance.SelectSlot(0); // hoe
            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(tile), "till");
            WorldSim.Instance.SelectSlot(3); // turnip seeds
            t.AssertEqual(ActionOutcome.Planted, WorldSim.Instance.UseSelectedItem(tile), "plant");
            WorldSim.Instance.SelectSlot(1); // watering can
            t.AssertEqual(ActionOutcome.Watered, WorldSim.Instance.UseSelectedItem(tile), "water");
            t.AssertEqual(1L, SaveService.Instance.Current.FlagDay(StoryKeys.FirstPlanting),
                "first planting stamped day 1");

            // Sleep into the crew morning. The beat fires straight out of the sleep
            // flow's return to Playing, so wait for the day advance — never for Playing.
            long dayBefore = Clock.Instance.Now.DayIndex;
            GameState.Instance.TransitionTo(GameState.Phase.Sleeping);
            t.Assert(await t.WaitUntil(() => Clock.Instance.Now.DayIndex > dayBefore, 10),
                "planted sleep advanced the day");
            t.AssertEqual(2L, SaveService.Instance.Current.FlagDay(StoryKeys.RoadCleared),
                "road_cleared stamped with the new day's index");
            AssertBlockade(t, main, present: false, "crew morning");

            // The dawn stamp landed BEFORE Main's autosave: the file on disk carries it.
            string autosavePath = Path.Combine(
                SaveService.SaveDirectory, SaveService.DefaultSlot + ".json");
            t.Assert(File.Exists(autosavePath), "morning autosave exists");
            JsonNode? stamp = JsonNode.Parse(File.ReadAllText(autosavePath))!
                ["StoryFlags"]?[StoryKeys.RoadCleared];
            t.Assert(stamp != null, "autosave file already contains intro.road_cleared");
            t.AssertEqual(2L, stamp!.GetValue<long>(), "autosave stamp is the new day's index");

            // Crew beat: deferred check after the sleep flow, 0.4 s of static staging.
            t.Assert(await t.WaitUntil(() => WorldSim.Instance.ActiveDialogue != null, 10),
                "crew beat started on the farm");
            t.AssertEqual("intro_crew_arrival", WorldSim.Instance.ActiveDialogue!.Def.Id,
                "the active dialogue is the crew arrival beat");

            // No clock advance while a dialogue runs.
            long minutesBefore = Clock.Instance.Now.TotalMinutes;
            await t.WaitFrames(30);
            t.AssertEqual(minutesBefore, Clock.Instance.Now.TotalMinutes,
                "clock frozen while ActiveDialogue != null");

            await DriveDialogueToCompletion(t, "crew arrival");
            t.AssertEqual(2L, SaveService.Instance.Current.FlagDay(StoryKeys.CrewArrivalDone),
                "crew_arrival_done stamped by the beat's terminal node");
            t.Assert(await t.WaitUntil(
                () => GameState.Instance.Current == GameState.Phase.Playing, 10),
                "Playing restored after the crew beat");

            // Travel farm -> town -> hall, all before 18:00: the meeting must NOT fire.
            t.Assert(Clock.Instance.Now.MinuteOfDay < IntroRules.MeetingStartMinuteOfDay,
                "still before 18:00 when heading to the hall");
            await TravelTo(t, MapIds.Town, "from_farm", "farm to town");
            await TravelTo(t, MapIds.TownHall, "entry", "town to hall");
            await t.WaitFrames(30);
            t.Assert(WorldSim.Instance.ActiveDialogue == null, "no meeting beat before 18:00");
            t.Assert(!SaveService.Instance.Current.HasFlag(StoryKeys.MeetingDone),
                "meeting not done before 18:00");

            // AdvanceMinutes ticks synchronously; a ten-minute tick inside the hall
            // at/after 18:00 schedules the deferred check that starts the meeting.
            Clock.Instance.AdvanceMinutes(
                IntroRules.MeetingStartMinuteOfDay + 10 - Clock.Instance.Now.MinuteOfDay);
            t.Assert(Clock.Instance.Now.MinuteOfDay >= IntroRules.MeetingStartMinuteOfDay,
                "advanced past 18:00 inside the hall");
            t.Assert(await t.WaitUntil(() => WorldSim.Instance.ActiveDialogue != null, 10),
                "meeting beat fired inside the hall after 18:00");
            t.AssertEqual("intro_town_meeting", WorldSim.Instance.ActiveDialogue!.Def.Id,
                "the active dialogue is the town meeting beat");

            await DriveDialogueToCompletion(t, "town meeting");
            t.AssertEqual(2L, SaveService.Instance.Current.FlagDay(StoryKeys.MeetingDone),
                "meeting_done stamped by the beat's terminal node");
            t.Assert(await t.WaitUntil(
                () => GameState.Instance.Current == GameState.Phase.Playing, 10),
                "Playing restored after the meeting");

            // Home and to bed; the sleep autosave freezes the completed intro.
            await TravelTo(t, MapIds.Town, "from_hall", "hall to town");
            await TravelTo(t, MapIds.Farm, "road", "town to farm");
            await SleepOneNight(t, "post-meeting night");

            // Reload from disk: stamps exact, nothing pends, nothing re-fires.
            t.AssertEqual(LoadResult.Ok, SaveService.Instance.Load(), "autosave loads back");
            GameData loaded = SaveService.Instance.Current;
            t.AssertEqual(1L, loaded.FlagDay(StoryKeys.FirstPlanting), "reloaded first_planting stamp");
            t.AssertEqual(2L, loaded.FlagDay(StoryKeys.RoadCleared), "reloaded road_cleared stamp");
            t.AssertEqual(2L, loaded.FlagDay(StoryKeys.CrewArrivalDone), "reloaded crew_arrival_done stamp");
            t.AssertEqual(2L, loaded.FlagDay(StoryKeys.MeetingDone), "reloaded meeting_done stamp");
            t.Assert(IntroRules.PendingBeat(loaded, Clock.Instance.Now, loaded.Player.MapId) == null,
                "PendingBeat re-derives null from the completed intro");
            await t.WaitFrames(30);
            t.Assert(WorldSim.Instance.ActiveDialogue == null, "no beat re-fires after the reload");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "still Playing after the reload");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Integration_MeetingMissedRecovers(TestContext t)
    {
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Jump straight to crew-done. Back-to-back stamps: by the time the
            // director's deferred check runs, CrewArrivalDone already blocks the crew beat.
            WorldSim.Instance.SetStoryFlag(StoryKeys.FirstPlanting);
            WorldSim.Instance.SetStoryFlag(StoryKeys.RoadCleared);
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);

            // Miss the meeting: let 18:00 pass on the farm, then sleep through the night.
            Clock.Instance.AdvanceMinutes(
                IntroRules.MeetingStartMinuteOfDay + 10 - Clock.Instance.Now.MinuteOfDay);
            await t.WaitFrames(10);
            t.Assert(WorldSim.Instance.ActiveDialogue == null,
                "no meeting beat fires on the farm");
            await SleepOneNight(t, "missed-meeting night");
            t.Assert(!SaveService.Instance.Current.HasFlag(StoryKeys.MeetingDone),
                "meeting still pending the next morning");

            // Next evening in the hall the beat fires — no day term, nothing was missed.
            await TravelTo(t, MapIds.Town, "from_farm", "farm to town");
            await TravelTo(t, MapIds.TownHall, "entry", "town to hall");
            await t.WaitFrames(30);
            t.Assert(WorldSim.Instance.ActiveDialogue == null,
                "no beat before 18:00 the next day");
            Clock.Instance.AdvanceMinutes(
                IntroRules.MeetingStartMinuteOfDay + 10 - Clock.Instance.Now.MinuteOfDay);
            t.Assert(await t.WaitUntil(() => WorldSim.Instance.ActiveDialogue != null, 10),
                "meeting beat fired the next evening");
            t.AssertEqual("intro_town_meeting", WorldSim.Instance.ActiveDialogue!.Def.Id,
                "the recovered beat is the town meeting");

            // Complete it so cleanup never frees Main mid-beat.
            await DriveDialogueToCompletion(t, "recovered meeting");
            t.Assert(SaveService.Instance.Current.HasFlag(StoryKeys.MeetingDone),
                "meeting_done stamped after the recovery");
            t.Assert(await t.WaitUntil(
                () => GameState.Instance.Current == GameState.Phase.Playing, 10),
                "Playing restored after the recovered meeting");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    // Debris blockade cells frozen by phase3-spec §6.
    private static readonly Vector2I[] RoadBlockCells =
    {
        new(36, 14), new(36, 15), new(37, 14), new(37, 15),
    };

    private static void AssertBlockade(TestContext t, Node main, bool present, string label)
    {
        MapRoot? map = FindCurrentMap(main);
        t.Assert(map != null, $"{label}: a current map exists under MapHost");
        var obstacles = map!.GetNodeOrNull<TileMapLayer>("Obstacles");
        t.Assert(obstacles != null, $"{label}: Obstacles layer exists");
        foreach (Vector2I cell in RoadBlockCells)
        {
            if (present)
            {
                t.Assert(obstacles!.GetCellSourceId(cell) != -1,
                    $"{label}: debris cell {cell} present");
            }
            else
            {
                t.AssertEqual(-1, obstacles!.GetCellSourceId(cell),
                    $"{label}: debris cell {cell} gone");
            }
        }
    }

    private static MapRoot? FindCurrentMap(Node main)
    {
        Node host = main.GetNode("World/MapHost");
        foreach (Node child in host.GetChildren())
        {
            if (child is MapRoot map && !map.IsQueuedForDeletion())
            {
                return map;
            }
        }
        return null;
    }

    // Requests travel through the bus and waits out Main's fade/swap flow.
    private static async Task TravelTo(TestContext t, string mapId, string spawnId, string label)
    {
        t.Assert(await t.WaitUntil(() => GameState.Instance.PlayerHasControl, 10),
            $"{label}: control returned before the travel request");
        t.Assert(WorldSim.Instance.RequestTravel(mapId, spawnId), $"{label}: request accepted");
        t.Assert(await t.WaitUntil(
            () => SaveService.Instance.Current.Player.MapId == mapId
                && GameState.Instance.Current == GameState.Phase.Playing,
            10),
            $"{label}: arrived and returned to Playing");
    }

    // Drives the active dialogue to completion from the outside, one pump per frame.
    // Choices are picked round-robin per node — the first visit takes choice 0 (the
    // crew fork), and hub-and-spoke graphs (the town meeting) reach their exit choice
    // wherever the copy puts it.
    private static async Task DriveDialogueToCompletion(TestContext t, string label)
    {
        var visits = new Dictionary<string, int>();
        for (int step = 0; step < 400 && WorldSim.Instance.ActiveDialogue != null; step++)
        {
            DialogueSession session = WorldSim.Instance.ActiveDialogue;
            if (session.AtChoices)
            {
                string node = string.Join("|", session.CurrentChoices.Select(c => c.NextNodeId));
                int seen = visits.GetValueOrDefault(node);
                visits[node] = seen + 1;
                WorldSim.Instance.ChooseDialogueOption(seen % session.CurrentChoices.Count);
            }
            else
            {
                WorldSim.Instance.AdvanceDialogue();
            }
            await t.WaitFrames(1);
        }
        t.Assert(WorldSim.Instance.ActiveDialogue == null, $"{label}: dialogue ran to completion");
    }

    // Runs Main's full sleep flow (fade -> day advance -> autosave -> fade) via the
    // Sleeping transition and waits for the morning.
    private static async Task SleepOneNight(TestContext t, string label)
    {
        long dayBefore = Clock.Instance.Now.DayIndex;
        GameState.Instance.TransitionTo(GameState.Phase.Sleeping);
        bool completed = await t.WaitUntil(
            () => Clock.Instance.Now.DayIndex > dayBefore
                && GameState.Instance.Current == GameState.Phase.Playing,
            10);
        t.Assert(completed, $"sleep flow completed within 10 s ({label})");
    }

    // --- Pure cell-state function (recomputed independently of the map code, spec §3) ---
    // FarmSoil atlas: column 0 = tilled-dry, column 1 = tilled-wet; wet iff the record was
    // watered on the CURRENT day at refresh time. Crops atlas: (column, row) =
    // (StageForDay(GrowthDay), index of the crop in CropDefs iteration order).

    private static readonly Vector2I EmptyCell = new(-1, -1);
    private static readonly Vector2I SoilDry = new(0, 0);
    private static readonly Vector2I SoilWet = new(1, 0);

    private static void AssertCells(TestContext t, TestMap map, Vector2I tile, string label)
    {
        TileRecord? record = SaveService.Instance.Current.GetMap("test_farm").GetTile(tile.X, tile.Y);
        t.AssertEqual(ExpectedSoilCell(record), CellOf(t, map, "FarmSoil", tile, label),
            $"{label}: FarmSoil cell matches the pure cell-state function");
        t.AssertEqual(ExpectedCropCell(record), CellOf(t, map, "Crops", tile, label),
            $"{label}: Crops cell matches the pure cell-state function");
    }

    private static Vector2I CellOf(TestContext t, TestMap map, string layerName, Vector2I tile, string label)
    {
        var layer = map.GetNodeOrNull<TileMapLayer>(layerName);
        t.Assert(layer != null, $"{label}: layer '{layerName}' exists");
        return layer!.GetCellAtlasCoords(tile);
    }

    private static Vector2I ExpectedSoilCell(TileRecord? record)
    {
        if (record == null || record.Kind != "tilled")
        {
            return EmptyCell;
        }
        return record.LastWateredDay == Clock.Instance.Now.DayIndex ? SoilWet : SoilDry;
    }

    private static Vector2I ExpectedCropCell(TileRecord? record)
    {
        if (record?.CropId == null)
        {
            return EmptyCell;
        }
        CropDef? def = CropDefs.TryGet(record.CropId);
        if (def == null)
        {
            return EmptyCell;
        }
        int row = 0;
        foreach (string id in CropDefs.All.Keys)
        {
            if (id == record.CropId)
            {
                return new Vector2I(def.StageForDay(record.GrowthDay), row);
            }
            row++;
        }
        return EmptyCell;
    }

    // Free the Main instance first so its event subscriptions are gone, then restore
    // global phase, save data, and clock time for the next test.
    private static async Task CleanupMainAsync(TestContext t, Node? main)
    {
        if (main != null && GodotObject.IsInstanceValid(main))
        {
            main.Free();
        }
        await t.WaitFrames(1);
        GameState.Instance.TransitionTo(GameState.Phase.Playing);
        SaveService.Instance.NewGame();

        // Delete the default-slot autosave so every Main boot starts from a known
        // no-save state — otherwise one test's autosave couples into the next boot.
        string path = Path.Combine(SaveService.SaveDirectory, SaveService.DefaultSlot + ".json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        if (File.Exists(path + ".tmp"))
        {
            File.Delete(path + ".tmp");
        }
    }
}
