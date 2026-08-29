using System.Text.Json.Nodes;
using Godot;
using TheHaunt.Core;
using TheHaunt.Player;
using TheHaunt.Systems;
using TheHaunt.UI;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class IntegrationTests
{
    // Bed Area2D position in FarmHouseMap: the centre of its footprint, tiles
    // (12,2)-(12,3). The bed moved indoors with the 3b farmhouse (phase3b-spec §4.4)
    // and on to the tile grid with the drawn art — the 16x32 sprite covers exactly
    // those two cells, where the placeholder sat half a cell low.
    private static readonly Vector2 BedPosition = new(200, 48);

    [SimTest]
    public static async Task Events_MapSwapStress(TestContext t)
    {
        // Catches leaked C# event subscriptions and stale WorldSim map registrations on
        // freed nodes: any handler still wired to the clock, or a freed map still resolved
        // by tool use or the overnight repaint, must crash a later cycle.
        try
        {
            SaveService.Instance.NewGame(); // clock -> 0, MapId "test_farm"
            TestKit.Fetch(SaveService.Instance.Current); // kit in hand, not in the barn chest
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
            service.NewGame(); // clock -> day 0
            TestKit.Fetch(service.Current); // kit in hand, not in the barn chest
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

            // The bed moved indoors with the 3b farmhouse: its vacated tiles are plain
            // tillable grass again — a deliberate decision, recorded here.
            t.Assert(map.IsTillable(8, 8), "vacated bed tile (8,8) is tillable again");
            t.Assert(map.IsTillable(8, 9), "vacated bed tile (8,9) is tillable again");
            t.Assert(!map.IsTillable(5, 5), "farmhouse facade wall (5,5) not tillable");
            t.Assert(!map.IsTillable(9, 4), "farmhouse facade wall (9,4) not tillable");
            t.Assert(!map.IsTillable(7, 7), "farmhouse door tile (7,7) not tillable");
            t.Assert(!map.IsTillable(17, 25), "the fallen log (17,25) not tillable");
            t.Assert(!map.IsTillable(12, 8), "sign tile (12,8) not tillable");
            t.Assert(!map.IsTillable(6, 8), "mailbox tile (6,8) not tillable");
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

            // Main's boot path seeds the field obstacles (EnsureObstacles between
            // AddChild and ApplyState) — the one call the shipping game relies on.
            MapState farm = SaveService.Instance.Current.GetMap(MapIds.Farm);
            t.Assert(farm.ObstaclesSeeded, "boot seeded the farm's obstacles");
            t.Assert(farm.Objects.Count > 0, $"and something grew ({farm.Objects.Count})");

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

            // The bed lives indoors now — enter the farmhouse first.
            await TravelTo(t, MapIds.FarmHouse, "entry", "into the farmhouse");

            // Stand just below the bed and face up so the probe reaches into it.
            // (The arrival spawn overlaps the interior door, so the probe may hold a
            // stale door focus for a frame — wait for the BED specifically.)
            player.GlobalPosition = BedPosition + new Vector2(0, 28);
            player.Probe.SetFacing(3);

            bool focused = await t.WaitUntil(() => player.Probe.Focused is Bed, 2);
            t.Assert(focused, "probe focused the bed within 2 s");
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
            service.NewGame(); // clock -> day 0
            TestKit.Fetch(service.Current); // kit in hand, not in the barn chest
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

            TestKit.Fetch(SaveService.Instance.Current); // kit in hand, not in the barn chest
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

            TestKit.Fetch(SaveService.Instance.Current); // kit in hand, not in the barn chest
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
            await TravelTo(t, MapIds.Town, "from_fork", "farm to town");
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
    public static async Task Integration_MailboxLetterQuestFlow(TestContext t)
    {
        // The whole first-morning chain: unread mail signalled at the box, the
        // farewell letter read through the probe + session UI, the quest handed out,
        // and the letter's own ask (till, plant, water) completing it with a toast.
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Story quiet so no beat steals control mid-test.
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            var maybeMailbox = FindNode<Mailbox>(main);
            t.Assert(maybeMailbox != null, "the farm builds its mailbox");
            t.Assert(maybeMailbox!.HasUnread, "first arrival: the mailbox signals unread mail");

            // Walk up: player on the door apron (7,8), facing left at the box (6,8).
            var player = main.GetNodeOrNull<PlayerController>("World/Player")!;
            player.GlobalPosition = new Vector2(7 * 16 + 8, 8 * 16 + 8);
            player.Probe.SetFacing(1);
            t.Assert(await t.WaitUntil(() => player.Probe.Focused is Mailbox, 2),
                "probe focuses the mailbox");
            t.AssertEqual("Mail", player.Probe.Focused!.PromptText, "focused prompt text");

            // E opens the session; the opening press must NOT also open a letter
            // (the _openedFrame guard) — the read stamp is the tell.
            await PressKey(t, Key.E);
            t.Assert(await t.WaitUntil(() => WorldSim.Instance.MailboxOpen, 2),
                "interact opened the mailbox session");
            t.AssertEqual(GameState.Phase.Menu, GameState.Instance.Current,
                "mailbox session runs in the Menu phase");
            var mailUi = FindNode<MailboxUi>(main)!;
            t.Assert(mailUi.Visible, "mail panel shown");
            t.Assert(AnyTextContains(mailUi, "From the previous owner"),
                "the farewell letter is listed");
            t.Assert(!SaveService.Instance.Current.HasFlag(StoryKeys.FarewellRead),
                "the opening press did not read the letter");

            // Second E opens the focused letter: body up, read stamped, quest handed
            // out, and the box's raised flag drops on the repaint the stamp triggers.
            await PressKey(t, Key.E);
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.HasFlag(StoryKeys.FarewellRead), 2),
                "opening the letter stamped its read flag");
            t.Assert(AnyTextContains(mailUi, "I hope you enjoy your new farm"),
                "the letter body is on screen");
            t.Assert(QuestRules.Active(QuestDefs.All[QuestDefs.FirstCrops], SaveService.Instance.Current),
                "reading the letter handed out first_crops");
            t.Assert(!maybeMailbox.HasUnread, "the read lowered the mailbox signal");
            var toast = FindNode<QuestToastUi>(main)!;
            t.Assert(await t.WaitUntil(() => AnyTextContains(toast, "New quest: Plant a Few Crops"), 2),
                "the hand-out toast shows");

            // Esc closes the session and gives Playing back.
            await PressKey(t, Key.Escape);
            t.Assert(await t.WaitUntil(
                () => !WorldSim.Instance.MailboxOpen
                    && GameState.Instance.Current == GameState.Phase.Playing, 2),
                "Esc closed the mailbox session");

            TestKit.Fetch(SaveService.Instance.Current); // kit in hand, not in the barn chest
            // The letter's ask, via the bus. Watering EMPTY tilled soil must not
            // complete it — the stamp needs a crop under the can.
            var tile = new Vector2I(20, 14); // dirt rectangle, obstacle-free
            WorldSim.Instance.SelectSlot(0); // hoe
            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(tile), "till");
            WorldSim.Instance.SelectSlot(1); // watering can
            t.AssertEqual(ActionOutcome.Watered, WorldSim.Instance.UseSelectedItem(tile), "pre-water");
            t.Assert(!SaveService.Instance.Current.HasFlag(StoryKeys.FirstWatering),
                "watering empty soil does not complete the quest");
            WorldSim.Instance.SelectSlot(3); // turnip seeds
            t.AssertEqual(ActionOutcome.Planted, WorldSim.Instance.UseSelectedItem(tile), "plant");
            WorldSim.Instance.SelectSlot(1);
            t.AssertEqual(ActionOutcome.Watered, WorldSim.Instance.UseSelectedItem(tile), "water the crop");
            t.Assert(SaveService.Instance.Current.HasFlag(StoryKeys.FirstWatering),
                "watering the planted tile stamped intro.first_watering");
            t.Assert(QuestRules.Completed(QuestDefs.All[QuestDefs.FirstCrops], SaveService.Instance.Current),
                "first_crops completed");
            // The banners QUEUE: the hand-out (3.5 s) is still up this soon after the
            // read, so the completion must wait its turn, not truncate it.
            t.Assert(AnyTextContains(toast, "New quest: Plant a Few Crops"),
                "the hand-out toast is still showing when the completion lands");
            t.Assert(await t.WaitUntil(
                () => AnyTextContains(toast, "Quest complete: Plant a Few Crops"), 10),
                "the completion toast shows after the hand-out toast expires");
        }
        finally
        {
            WorldSim.Instance.CloseMailbox();
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task QuestLog_ToggleGating(TestContext t)
    {
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Story quiet so nothing steals control mid-test.
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            var maybeLog = FindNode<QuestLogUi>(main);
            t.Assert(maybeLog != null, "QuestLog exists under Main's UI");
            QuestLogUi log = maybeLog!;
            t.Assert(!log.Visible, "quest log hidden at boot");

            // Playing: J toggles both ways, non-modal, and the empty state shows.
            await PressKey(t, Key.J);
            t.Assert(await t.WaitUntil(() => log.Visible, 2), "J shows the log in Playing");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "non-modal: showing the log keeps Playing");
            t.Assert(AnyTextContains(log, "Nothing yet."), "empty log says so");
            await PressKey(t, Key.J);
            t.Assert(await t.WaitUntil(() => !log.Visible, 2), "J hides the log again");

            // Hand out the quest through the bus; the log lists it with its ask.
            t.Assert(WorldSim.Instance.OpenMailbox(), "mailbox session for the hand-out");
            t.Assert(WorldSim.Instance.ReadLetter(LetterDefs.Farewell), "read the farewell");
            WorldSim.Instance.CloseMailbox();
            await PressKey(t, Key.J);
            t.Assert(await t.WaitUntil(() => log.Visible, 2), "log reopened");
            t.Assert(AnyTextContains(log, "Plant a Few Crops"), "active quest listed");
            t.Assert(AnyTextContains(log, "Till the soil, plant the seeds, then water them."),
                "active quest carries the letter's ask");
            await PressKey(t, Key.J);
            t.Assert(await t.WaitUntil(() => !log.Visible, 2), "log closed for the gating sweep");

            // Dialogue: inert.
            t.Assert(WorldSim.Instance.StartDialogue("foreman_wait"), "dialogue started");
            await PressKey(t, Key.J);
            await t.WaitFrames(5);
            t.Assert(!log.Visible, "J inert during dialogue");
            await DriveDialogueToCompletion(t, "quest log gating dialogue");
            t.Assert(await t.WaitUntil(() => GameState.Instance.PlayerHasControl, 5),
                "control back after the dialogue");

            // Menu: inert.
            t.Assert(WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest), "chest opened");
            await PressKey(t, Key.J);
            await t.WaitFrames(5);
            t.Assert(!log.Visible, "J inert during a Menu session");
            WorldSim.Instance.CloseStorage();

            // Paused: inert.
            GameState.Instance.TransitionTo(GameState.Phase.Paused);
            await PressKey(t, Key.J);
            await t.WaitFrames(5);
            t.Assert(!log.Visible, "J inert while paused");
            GameState.Instance.TransitionTo(GameState.Phase.Playing);

            // Losing control force-hides an open log, and it stays hidden after.
            await PressKey(t, Key.J);
            t.Assert(await t.WaitUntil(() => log.Visible, 2), "log shown before control loss");
            t.Assert(WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest), "chest steals control");
            t.Assert(!log.Visible, "control loss force-hid the log");
            WorldSim.Instance.CloseStorage();
            await t.WaitFrames(2);
            t.Assert(!log.Visible, "the log stays hidden after control returns");
        }
        finally
        {
            WorldSim.Instance.CloseStorage();
            WorldSim.Instance.CloseMailbox();
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Integration_MeetingMissedWakesAtHall(TestContext t)
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

            // Miss the meeting: let 18:00 pass on the farm, then go to bed. The wake
            // relocates to the hall behind the sleep fade, and the overslept beat
            // fires straight out of the sleep flow's return to Playing — wait for
            // the day advance + the beat, never for Playing.
            Clock.Instance.AdvanceMinutes(
                IntroRules.MeetingStartMinuteOfDay + 10 - Clock.Instance.Now.MinuteOfDay);
            await t.WaitFrames(10);
            t.Assert(WorldSim.Instance.ActiveDialogue == null,
                "no meeting beat fires on the farm");
            long dayBefore = Clock.Instance.Now.DayIndex;
            GameState.Instance.TransitionTo(GameState.Phase.Sleeping);
            t.Assert(await t.WaitUntil(() => Clock.Instance.Now.DayIndex > dayBefore, 10),
                "missed-meeting sleep advanced the day");

            t.AssertEqual(MapIds.TownHall, SaveService.Instance.Current.Player.MapId,
                "the wake relocated the player to the town hall");
            t.Assert(SaveService.Instance.Current.HasFlag(StoryKeys.Overslept),
                "intro.overslept stamped by the relocated wake");

            // The relocation and flag landed BEFORE the morning autosave, so a
            // quit-and-reload replays the same relocated morning.
            string autosavePath = Path.Combine(
                SaveService.SaveDirectory, SaveService.DefaultSlot + ".json");
            JsonNode saved = JsonNode.Parse(File.ReadAllText(autosavePath))!;
            t.AssertEqual(MapIds.TownHall, (string?)saved["Player"]?["MapId"],
                "the autosave already carries the hall as the player's map");
            t.Assert(saved["StoryFlags"]?[StoryKeys.Overslept] != null,
                "the autosave already carries intro.overslept");

            t.Assert(await t.WaitUntil(() => WorldSim.Instance.ActiveDialogue != null, 10),
                "the meeting beat fired out of the relocated wake");
            t.AssertEqual("intro_town_meeting_overslept", WorldSim.Instance.ActiveDialogue!.Def.Id,
                "the recovered beat is the overslept meeting variant");
            t.AssertEqual("", WorldSim.Instance.ActiveDialogue!.CurrentLine.SpeakerRole,
                "the variant opens on the offscreen-walk narration");

            // Dawn hour, mayor at the podium anyway: the overslept schedule row, not
            // the evening window, staged this meeting.
            t.AssertEqual(new NpcPlacement(MapIds.TownHall, 20, 6, 0),
                NpcSchedules.Resolve(
                    NpcDefs.All["mayor"], SaveService.Instance.Current, Clock.Instance.Now)!.Value,
                "the mayor holds the podium on the overslept morning");

            // Complete it so cleanup never frees Main mid-beat.
            await DriveDialogueToCompletion(t, "overslept meeting");
            t.Assert(SaveService.Instance.Current.HasFlag(StoryKeys.MeetingDone),
                "meeting_done stamped by the overslept variant");
            t.Assert(await t.WaitUntil(
                () => GameState.Instance.Current == GameState.Phase.Playing, 10),
                "Playing restored after the recovered meeting");
            t.Assert(!IntroRules.WakesAtTownHall(SaveService.Instance.Current),
                "attended: the next bedtime stays home");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Integration_ChestOpenTransferClose(TestContext t)
    {
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");
            var maybePlayer = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(maybePlayer != null, "World/Player exists after boot");
            PlayerController player = maybePlayer!;

            // Story quiet (no planting happens here, but cheap insurance).
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            // Seed both sides BEFORE the chest opens: chest slot 0 is the
            // opening-press canary (a leaked press would transfer it).
            GameData data = SaveService.Instance.Current;
            StorageData chest = data.GetStorage(StorageIds.FarmHouseChest);
            chest.Slots[0] = new ItemStackRecord { ItemId = "turnip", Count = 3 };
            data.Player.Inventory.Slots[5] = new ItemStackRecord { ItemId = "greenbean", Count = 2 };

            await TravelTo(t, MapIds.FarmHouse, "entry", "into the farmhouse");

            // Stand under the chest at (2,2) and face up so the probe focuses it.
            // (The arrival spawn overlaps the interior door — wait for the CHEST
            // specifically, not just any focus.)
            player.GlobalPosition = new Vector2(40, 56); // tile (2,3) center
            player.Probe.SetFacing(3);
            t.Assert(await t.WaitUntil(() => player.Probe.Focused is Chest, 2),
                "probe focused the chest");
            t.AssertEqual("Open", player.Probe.Focused!.PromptText, "chest prompt text");

            // Open with a real interact press through the input pipeline.
            await PressKey(t, Key.E);
            t.Assert(await t.WaitUntil(() => WorldSim.Instance.OpenStorageId != null, 2),
                "interact press opened the chest session");
            t.AssertEqual(StorageIds.FarmHouseChest, WorldSim.Instance.OpenStorageId,
                "open storage id");
            t.AssertEqual(GameState.Phase.Menu, GameState.Instance.Current,
                "phase in Menu while the chest is open");

            // The opening press must NOT have doubled as a transfer press on the
            // focused chest slot 0 (the DialogueUi _openedFrame pattern).
            await t.WaitFrames(2);
            t.Assert(chest.Slots[0] != null && chest.Slots[0]!.Count == 3,
                "opening press did not transfer chest slot 0");

            // Chest -> inventory: a second interact press moves the focused slot 0 stack.
            await PressKey(t, Key.E);
            t.Assert(await t.WaitUntil(() => chest.Slots[0] == null, 2),
                "interact press transferred the focused chest stack");
            t.AssertEqual(3, data.Player.Inventory.CountOf("turnip"),
                "turnips arrived in the inventory");

            // Inventory -> chest through the same model op the slot buttons call (grid
            // focus navigation is not simulated headlessly; the assertions are the contract).
            t.Assert(WorldSim.Instance.TransferToStorage(StorageIds.FarmHouseChest, 5),
                "inventory stack deposited");
            t.Assert(data.Player.Inventory.SlotAt(5) == null, "inventory source slot vacated");
            t.AssertEqual(2, StackOps.CountOf(chest.Slots, "greenbean"), "greenbeans in the chest");

            // Esc closes: session cleared, straight back to Playing...
            await PressKey(t, Key.Escape);
            t.Assert(await t.WaitUntil(
                () => WorldSim.Instance.OpenStorageId == null
                    && GameState.Instance.Current == GameState.Phase.Playing, 2),
                "Esc closed the chest back to Playing");

            // ...and the closing press is fully swallowed: the same Esc never reaches
            // the pause menu, nothing re-opens, and no stray press moves an item on
            // the first control frame — the closing-press regression pin.
            await t.WaitFrames(5);
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "closing Esc never reached the pause menu");
            t.Assert(WorldSim.Instance.OpenStorageId == null, "chest did not re-open");
            t.AssertEqual(3, data.Player.Inventory.CountOf("turnip"),
                "inventory stable across the close");
            t.AssertEqual(2, StackOps.CountOf(chest.Slots, "greenbean"),
                "chest stable across the close");
        }
        finally
        {
            WorldSim.Instance.CloseStorage();
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Integration_MorningReportFlow(TestContext t)
    {
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Story quiet: no beat may interleave with the report card.
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            var maybeReport = main.GetNodeOrNull<OvernightReportUi>("UI/OvernightReport");
            t.Assert(maybeReport != null, "UI/OvernightReport exists");
            OvernightReportUi report = maybeReport!;
            t.Assert(!report.Visible, "no card before any sleep");

            // Ship one stack of turnips.
            InventoryData inv = SaveService.Instance.Current.Player.Inventory;
            inv.Slots[5] = new ItemStackRecord { ItemId = "turnip", Count = 5 };
            WorldSim.Instance.SelectSlot(5);
            t.Assert(WorldSim.Instance.DepositSelectedToBin(), "deposit turnips");
            long moneyBefore = SaveService.Instance.Current.Player.Money;
            long expected = 5L * ItemDefs.Get("turnip").SellPrice; // 200

            // Sleep: the card must interpose while the phase is STILL Sleeping.
            long dayBefore = Clock.Instance.Now.DayIndex;
            GameState.Instance.TransitionTo(GameState.Phase.Sleeping);
            t.Assert(await t.WaitUntil(() => Clock.Instance.Now.DayIndex > dayBefore, 10),
                "sleep advanced the day");
            t.Assert(await t.WaitUntil(() => report.Visible, 10), "report card shown");
            t.AssertEqual(GameState.Phase.Sleeping, GameState.Instance.Current,
                "phase still Sleeping while the card is up");

            // Copy pins (§5.2): the sold line and the total row.
            t.Assert(AnyTextContains(report, "Turnip x5"), "sold line lists 'Turnip x5'");
            t.Assert(AnyTextContains(report, $"+{expected}g"), "total row shows the exact total");

            // Money was credited AND autosaved BEFORE the card: quitting mid-card
            // loses only the popup, never money.
            t.AssertEqual(moneyBefore + expected, SaveService.Instance.Current.Player.Money,
                "money credited before the card");
            t.Assert(SaveService.Instance.SaveFileExists(), "autosave already on disk under the card");
            string autosavePath = Path.Combine(
                SaveService.SaveDirectory, SaveService.DefaultSlot + ".json");
            JsonNode? savedMoney = JsonNode.Parse(File.ReadAllText(autosavePath))!["Player"]?["Money"];
            t.Assert(savedMoney != null && savedMoney.GetValue<long>() == moneyBefore + expected,
                "the autosave already carries the credited money");

            report.Dismiss();
            t.Assert(await t.WaitUntil(
                () => GameState.Instance.Current == GameState.Phase.Playing, 10),
                "Dismiss released the sleep flow to Playing");
            t.Assert(!report.Visible, "card hidden after dismissal");

            // A second, empty-bin night reaches Playing WITHOUT any dismissal —
            // proof that zero-proceeds mornings show nothing.
            long day2 = Clock.Instance.Now.DayIndex;
            GameState.Instance.TransitionTo(GameState.Phase.Sleeping);
            t.Assert(await t.WaitUntil(
                () => Clock.Instance.Now.DayIndex > day2
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "empty-bin sleep reached Playing on its own");
            t.Assert(!report.Visible, "zero-proceeds morning shows no card");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Integration_SleepInHouseCrewBeatOnExit(TestContext t)
    {
        // The 3b normal case: plant, sleep INDOORS, and meet the crew only on
        // stepping outside — dialogue-driven end to end, deliberately NOT pre-stamped.
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            TestKit.Fetch(SaveService.Instance.Current); // kit in hand, not in the barn chest
            // First planting on day 0: the road clears at the next dawn.
            var tile = new Vector2I(20, 14); // dirt rectangle, obstacle-free
            WorldSim.Instance.SelectSlot(0); // hoe
            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(tile), "till");
            WorldSim.Instance.SelectSlot(3); // turnip seeds
            t.AssertEqual(ActionOutcome.Planted, WorldSim.Instance.UseSelectedItem(tile), "plant");

            // Head indoors and sleep there. The farmhouse is not the farm exterior,
            // so the crew beat cannot fire out of the sleep flow's return to Playing.
            await TravelTo(t, MapIds.FarmHouse, "entry", "into the farmhouse");
            long dayBefore = Clock.Instance.Now.DayIndex;
            GameState.Instance.TransitionTo(GameState.Phase.Sleeping);
            t.Assert(await t.WaitUntil(
                () => Clock.Instance.Now.DayIndex > dayBefore
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "indoor sleep reached the morning (empty bin: no card)");

            t.AssertEqual(1L, SaveService.Instance.Current.FlagDay(StoryKeys.RoadCleared),
                "road cleared at the indoor dawn");
            await t.WaitFrames(30);
            t.Assert(WorldSim.Instance.ActiveDialogue == null,
                "no crew beat indoors");
            t.Assert(!SaveService.Instance.Current.HasFlag(StoryKeys.CrewArrivalDone),
                "crew arrival still pending");

            // Step outside: the beat fires at the door. It starts straight out of the
            // travel flow's return to Playing, so wait for the arrival + the beat —
            // never for Playing.
            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Farm, "house_door"),
                "travel out the front door accepted");
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.Farm, 10),
                "back on the farm exterior");
            t.Assert(await t.WaitUntil(() => WorldSim.Instance.ActiveDialogue != null, 10),
                "crew beat fired on stepping outside");
            t.AssertEqual("intro_crew_arrival", WorldSim.Instance.ActiveDialogue!.Def.Id,
                "the active dialogue is the crew arrival beat");

            await DriveDialogueToCompletion(t, "crew arrival at the door");
            t.AssertEqual(1L, SaveService.Instance.Current.FlagDay(StoryKeys.CrewArrivalDone),
                "crew_arrival_done stamped by the beat's terminal node");
            t.Assert(await t.WaitUntil(
                () => GameState.Instance.Current == GameState.Phase.Playing, 10),
                "Playing restored after the beat");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Help_ToggleGating(TestContext t)
    {
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Story quiet so nothing steals control mid-test.
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            var maybeHelp = FindNode<HelpPanel>(main);
            t.Assert(maybeHelp != null, "HelpPanel exists under Main's UI");
            HelpPanel help = maybeHelp!;
            t.Assert(!help.Visible, "help panel hidden at boot");

            // Playing: Tab toggles both ways, and the phase never moves (non-modal).
            await PressKey(t, Key.Tab);
            t.Assert(await t.WaitUntil(() => help.Visible, 2), "Tab shows the panel in Playing");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "non-modal: showing the panel keeps Playing");
            await PressKey(t, Key.Tab);
            t.Assert(await t.WaitUntil(() => !help.Visible, 2), "Tab hides the panel again");

            // Dialogue: inert.
            t.Assert(WorldSim.Instance.StartDialogue("foreman_wait"), "dialogue started");
            await t.WaitFrames(2);
            await PressKey(t, Key.Tab);
            await t.WaitFrames(5);
            t.Assert(!help.Visible, "Tab inert during dialogue");
            await DriveDialogueToCompletion(t, "help-gating dialogue");
            t.Assert(await t.WaitUntil(() => GameState.Instance.PlayerHasControl, 5),
                "control restored after the dialogue");

            // Menu (chest session): inert.
            t.Assert(WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest),
                "chest session opened");
            await PressKey(t, Key.Tab);
            await t.WaitFrames(5);
            t.Assert(!help.Visible, "Tab inert in Menu");
            WorldSim.Instance.CloseStorage();
            await t.WaitFrames(2);

            // Paused: inert.
            GameState.Instance.TransitionTo(GameState.Phase.Paused);
            await PressKey(t, Key.Tab);
            await t.WaitFrames(5);
            t.Assert(!help.Visible, "Tab inert while paused");
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            await t.WaitFrames(2);

            // Losing control force-hides an open panel — it can never underlap a modal.
            await PressKey(t, Key.Tab);
            t.Assert(await t.WaitUntil(() => help.Visible, 2), "panel re-shown in Playing");
            t.Assert(WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest),
                "modal session opens over the panel");
            t.Assert(!help.Visible, "control loss force-hid the panel");
            WorldSim.Instance.CloseStorage();
            await t.WaitFrames(2);
            t.Assert(!help.Visible, "panel stays hidden after the modal closes");
        }
        finally
        {
            WorldSim.Instance.CloseStorage();
            await CleanupMainAsync(t, main);
        }
    }

    // Debris blockade cells — phase3-spec §6, amended 2026-08-27: the road leaves south.
    private static readonly Vector2I[] RoadBlockCells =
    {
        new(36, 26), new(36, 27), new(37, 26), new(37, 27),
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

    // Runs Main's full sleep flow (fade -> day advance -> autosave -> fade -> report)
    // via the Sleeping transition and waits for the morning. A shipping night raises
    // the overnight report card, which holds the flow in Sleeping until dismissed —
    // the helper dismisses it programmatically (only the shipping night shows one).
    private static async Task SleepOneNight(TestContext t, string label)
    {
        long dayBefore = Clock.Instance.Now.DayIndex;
        GameState.Instance.TransitionTo(GameState.Phase.Sleeping);
        t.Assert(await t.WaitUntil(() => Clock.Instance.Now.DayIndex > dayBefore, 10),
            $"sleep advanced the day ({label})");

        OvernightReportUi? report = FindNode<OvernightReportUi>(t.Host);
        bool settled = await t.WaitUntil(
            () => GameState.Instance.Current == GameState.Phase.Playing
                || (report != null && report.Visible),
            10);
        t.Assert(settled, $"morning settled into Playing or the report card ({label})");
        if (report != null && report.Visible)
        {
            report.Dismiss();
        }
        bool completed = await t.WaitUntil(
            () => GameState.Instance.Current == GameState.Phase.Playing, 10);
        t.Assert(completed, $"sleep flow completed within 10 s ({label})");
    }

    // Depth-first search by node type — 3b UI nodes are found structurally so the
    // tests never hard-code more scene paths than the spec freezes.
    private static T? FindNode<T>(Node root) where T : class
    {
        if (root is T match)
        {
            return match;
        }
        foreach (Node child in root.GetChildren())
        {
            if (FindNode<T>(child) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    // Injects a full press+release of a physical key through the real input pipeline
    // (Input singleton + viewport dispatch), waiting frames between the edges so both
    // just-pressed polling and _UnhandledInput observe the press.
    private static async Task PressKey(TestContext t, Key physicalKey)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = physicalKey, Pressed = true });
        await t.WaitFrames(2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = physicalKey, Pressed = false });
        await t.WaitFrames(2);
    }

    // Finds a Label/RichTextLabel whose text contains the fragment — copy pins for
    // code-built UI cards without depending on their internal layout.
    private static bool AnyTextContains(Node root, string fragment)
    {
        string? text = root switch
        {
            Label label => label.Text,
            RichTextLabel rich => rich.Text,
            Button button => button.Text,   // list rows (mailbox letters) are Buttons
            _ => null,
        };
        if (text != null && text.Contains(fragment))
        {
            return true;
        }
        foreach (Node child in root.GetChildren())
        {
            if (AnyTextContains(child, fragment))
            {
                return true;
            }
        }
        return false;
    }

    // --- Pure cell-state function (recomputed independently of the map code, spec §3) ---
    // FarmSoil atlas: row 1 dry, row 2 wet, column chosen by which sides are still grass;
    // wet iff the record was watered on the CURRENT day at refresh time. Crops atlas:
    // (column, row) = (StageForDay(GrowthDay), index of the crop in CropDefs order).

    private static readonly Vector2I EmptyCell = new(-1, -1);
    // Every tile these tests work is tilled on its own, so column 0 — "grass on every
    // side" — is the configuration it takes. FarmArtTests covers the neighbour cases.
    private static readonly Vector2I SoilDry = new(0, 1);
    private static readonly Vector2I SoilWet = new(0, 2);

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
