using System.Text.Json.Nodes;
using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class StoryTests
{
    [SimTest]
    public static void Story_FlagStampAndIdempotence(TestContext t)
    {
        var data = new GameData();
        t.Assert(!data.HasFlag(StoryKeys.FirstPlanting), "fresh data has no flags");
        t.AssertEqual(-1L, data.FlagDay(StoryKeys.FirstPlanting), "FlagDay -1 when absent");

        t.Assert(data.TrySetFlag(StoryKeys.FirstPlanting, 3), "first set returns true");
        t.Assert(data.HasFlag(StoryKeys.FirstPlanting), "flag present after set");
        t.AssertEqual(3L, data.FlagDay(StoryKeys.FirstPlanting), "stamp is the set day");

        t.Assert(!data.TrySetFlag(StoryKeys.FirstPlanting, 9), "second set refused (only-if-absent)");
        t.AssertEqual(3L, data.FlagDay(StoryKeys.FirstPlanting), "original stamp preserved");
    }

    [SimTest]
    public static void Story_RoadClearRules(TestContext t)
    {
        // No planting: the road stays blocked forever — no timer clears it.
        var idle = new GameData();
        for (long dawn = 1; dawn <= 10; dawn++)
        {
            t.AssertEqual(0, IntroRules.FlagsToSetOnDayStarted(idle, dawn).Count,
                $"no planting: dawn {dawn} sets nothing");
        }

        // Plant day 0: cleared exactly at dawn 1, not at dawn 0.
        var early = new GameData();
        early.TrySetFlag(StoryKeys.FirstPlanting, 0);
        t.AssertEqual(0, IntroRules.FlagsToSetOnDayStarted(early, 0).Count,
            "plant day 0: dawn 0 sets nothing (needs newDayIndex > plant day)");
        IReadOnlyList<string> atDawn1 = IntroRules.FlagsToSetOnDayStarted(early, 1);
        t.AssertEqual(1, atDawn1.Count, "plant day 0: dawn 1 sets exactly one flag");
        t.AssertEqual(StoryKeys.RoadCleared, atDawn1[0], "plant day 0: dawn 1 sets RoadCleared");

        // Plant day 4: dawns 1..4 do nothing, dawn 5 clears.
        var late = new GameData();
        late.TrySetFlag(StoryKeys.FirstPlanting, 4);
        for (long dawn = 1; dawn <= 4; dawn++)
        {
            t.AssertEqual(0, IntroRules.FlagsToSetOnDayStarted(late, dawn).Count,
                $"plant day 4: dawn {dawn} sets nothing");
        }
        IReadOnlyList<string> atDawn5 = IntroRules.FlagsToSetOnDayStarted(late, 5);
        t.AssertEqual(1, atDawn5.Count, "plant day 4: dawn 5 sets exactly one flag");
        t.AssertEqual(StoryKeys.RoadCleared, atDawn5[0], "plant day 4: dawn 5 sets RoadCleared");

        // Already-cleared: evaluated at EVERY dawn, idempotent.
        ApplyDawn(late, 5);
        t.Assert(late.HasFlag(StoryKeys.RoadCleared), "dawn application stamped RoadCleared");
        for (long dawn = 6; dawn <= 9; dawn++)
        {
            t.AssertEqual(0, IntroRules.FlagsToSetOnDayStarted(late, dawn).Count,
                $"already cleared: dawn {dawn} sets nothing");
        }

        // Post-midnight planting: 1:30 AM of day N is still DayIndex N (the day runs
        // 6:00 -> 26:00 monotonic), so the stamp is the ENDING day and the road clears
        // at the very next dawn (N+1). Intended per spec §1.3.
        var night = new GameData();
        var pastMidnight = new GameTime(7 * GameTime.MinutesPerDay + 1170); // day 7, 1:30 AM
        t.AssertEqual(7L, pastMidnight.DayIndex, "past-midnight time still reads as day 7");
        night.TrySetFlag(StoryKeys.FirstPlanting, pastMidnight.DayIndex);
        IReadOnlyList<string> nextDawn = IntroRules.FlagsToSetOnDayStarted(night, 8);
        t.AssertEqual(1, nextDawn.Count, "post-midnight plant clears at the next dawn");
        t.AssertEqual(StoryKeys.RoadCleared, nextDawn[0], "post-midnight plant sets RoadCleared");
    }

    [SimTest]
    public static void Story_RulesTotalOnHostileFlags(TestContext t)
    {
        // Both rule functions must be total: every 2^4 intro-flag combination, plus an
        // unknown key, across hostile stamps, must degrade to skip-or-replay, never throw.
        string[] introFlags =
        {
            StoryKeys.FirstPlanting, StoryKeys.RoadCleared,
            StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
        };
        long[] stamps = { 0, 1, 999999 }; // 0 also covers the negative-stamp clamp repair
        string[] maps = { MapIds.Farm, MapIds.Town, MapIds.TownHall, "unknown_map" };
        int[] minutes = { 0, 719, 720, 1199 };

        int evaluated = 0;
        foreach (long stamp in stamps)
        {
            for (int combo = 0; combo < 16; combo++)
            {
                var data = new GameData();
                for (int bit = 0; bit < introFlags.Length; bit++)
                {
                    if ((combo & (1 << bit)) != 0)
                    {
                        data.TrySetFlag(introFlags[bit], stamp);
                    }
                }
                data.TrySetFlag("future.mystery_flag", stamp);

                foreach (long dawn in new[] { 0L, 1L, stamp + 1 })
                {
                    IReadOnlyList<string> flags = IntroRules.FlagsToSetOnDayStarted(data, dawn);
                    t.Assert(flags != null, $"combo {combo} stamp {stamp}: dawn result non-null");
                }
                foreach (string map in maps)
                {
                    foreach (int minute in minutes)
                    {
                        var now = new GameTime(3 * GameTime.MinutesPerDay + minute);
                        IntroRules.PendingBeat(data, now, map); // any throw fails the test
                        evaluated++;
                    }
                }
            }
        }
        t.AssertEqual(stamps.Length * 16 * maps.Length * minutes.Length, evaluated,
            "full hostile matrix evaluated");
    }

    [SimTest]
    public static void Story_PendingBeatMatrix(TestContext t)
    {
        int[] minutes = { 719, 720, 1199 };
        string[] maps = { MapIds.Farm, MapIds.Town, MapIds.TownHall };

        // Crew pending: road cleared, arrival beat not done — farm only, any hour.
        var crewPending = new GameData();
        crewPending.TrySetFlag(StoryKeys.FirstPlanting, 1);
        crewPending.TrySetFlag(StoryKeys.RoadCleared, 2);
        foreach (string map in maps)
        {
            foreach (int minute in minutes)
            {
                var now = new GameTime(2 * GameTime.MinutesPerDay + minute);
                StoryBeatId? expected = map == MapIds.Farm ? StoryBeatId.CrewArrival : null;
                t.AssertEqual(expected, IntroRules.PendingBeat(crewPending, now, map),
                    $"crew pending: map '{map}' minute {minute}");
            }
        }

        // Meeting pending: crew done — town hall only, at/after 18:00 (720 inclusive).
        var meetingPending = new GameData();
        meetingPending.TrySetFlag(StoryKeys.FirstPlanting, 1);
        meetingPending.TrySetFlag(StoryKeys.RoadCleared, 2);
        meetingPending.TrySetFlag(StoryKeys.CrewArrivalDone, 2);
        foreach (string map in maps)
        {
            foreach (int minute in minutes)
            {
                var now = new GameTime(2 * GameTime.MinutesPerDay + minute);
                StoryBeatId? expected =
                    map == MapIds.TownHall && minute >= IntroRules.MeetingStartMinuteOfDay
                        ? StoryBeatId.TownMeeting
                        : null;
                t.AssertEqual(expected, IntroRules.PendingBeat(meetingPending, now, map),
                    $"meeting pending: map '{map}' minute {minute}");
            }
        }

        // Everything done: nothing pends anywhere, ever.
        var done = new GameData();
        done.TrySetFlag(StoryKeys.FirstPlanting, 1);
        done.TrySetFlag(StoryKeys.RoadCleared, 2);
        done.TrySetFlag(StoryKeys.CrewArrivalDone, 2);
        done.TrySetFlag(StoryKeys.MeetingDone, 2);
        foreach (string map in maps)
        {
            foreach (int minute in minutes)
            {
                var now = new GameTime(9 * GameTime.MinutesPerDay + minute);
                t.Assert(IntroRules.PendingBeat(done, now, map) == null,
                    $"all done: nothing pends on '{map}' at minute {minute}");
            }
        }
    }

    [SimTest]
    public static void Story_MeetingRecursNightly(TestContext t)
    {
        // No day-equality term anywhere: the meeting re-pends EVERY evening from 18:00
        // until attended — missing the first night loses nothing.
        var data = new GameData();
        data.TrySetFlag(StoryKeys.FirstPlanting, 1);
        data.TrySetFlag(StoryKeys.RoadCleared, 2);
        data.TrySetFlag(StoryKeys.CrewArrivalDone, 2);

        for (long day = 3; day <= 5; day++)
        {
            var morning = new GameTime(day * GameTime.MinutesPerDay + 300);
            t.Assert(IntroRules.PendingBeat(data, morning, MapIds.TownHall) == null,
                $"day {day}: no meeting in the morning");
            var evening = new GameTime(day * GameTime.MinutesPerDay + 900);
            t.AssertEqual((StoryBeatId?)StoryBeatId.TownMeeting,
                IntroRules.PendingBeat(data, evening, MapIds.TownHall),
                $"day {day}: meeting pends again in the evening");
        }

        data.TrySetFlag(StoryKeys.MeetingDone, 5);
        var after = new GameTime(6 * GameTime.MinutesPerDay + 900);
        t.Assert(IntroRules.PendingBeat(data, after, MapIds.TownHall) == null,
            "attended: the meeting never pends again");
    }

    [SimTest]
    public static async Task Story_RoadRepaintOnFlag(TestContext t)
    {
        SaveService service = SaveService.Instance;
        TestMap? map = null;
        var events = new List<(string FlagId, long Day)>();
        Action<string, long> onFlagSet = (flagId, day) => events.Add((flagId, day));
        WorldSim.Instance.StoryFlagSet += onFlagSet;
        try
        {
            service.NewGame();
            map = new TestMap { MapId = MapIds.Farm };
            t.Host.AddChild(map);
            await t.WaitFrames(1);
            map.ApplyState(service.Current.GetMap(MapIds.Farm)); // hydrate like Main.LoadMap

            var obstacles = map.GetNodeOrNull<TileMapLayer>("Obstacles");
            t.Assert(obstacles != null, "Obstacles layer exists");
            foreach (Vector2I cell in RoadBlockCells)
            {
                t.Assert(obstacles!.GetCellSourceId(cell) != -1,
                    $"blockade cell {cell} painted while the road is blocked");
            }

            t.Assert(WorldSim.Instance.SetStoryFlag(StoryKeys.RoadCleared),
                "SetStoryFlag(RoadCleared) reports a new set");
            foreach (Vector2I cell in RoadBlockCells)
            {
                t.AssertEqual(-1, obstacles!.GetCellSourceId(cell),
                    $"blockade cell {cell} erased by the flag repaint");
            }
            t.AssertEqual(1, events.Count, "StoryFlagSet fired exactly once");
            t.AssertEqual(StoryKeys.RoadCleared, events[0].FlagId, "event flag id");
            t.AssertEqual(Clock.Instance.Now.DayIndex, events[0].Day, "event day stamp");

            t.Assert(!WorldSim.Instance.SetStoryFlag(StoryKeys.RoadCleared),
                "second SetStoryFlag returns false");
            t.AssertEqual(1, events.Count, "no event on the refused second set");
        }
        finally
        {
            WorldSim.Instance.StoryFlagSet -= onFlagSet;
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
    public static async Task Story_PlantingSetsFlagOnce(TestContext t)
    {
        SaveService service = SaveService.Instance;
        TestMap? map = null;
        try
        {
            service.NewGame(); // clock -> day 0, starter kit, MapId "test_farm"
            map = new TestMap { MapId = MapIds.Farm };
            t.Host.AddChild(map);
            await t.WaitFrames(1);

            var tile = new Vector2I(20, 14); // dirt rectangle, obstacle-free
            WorldSim.Instance.SelectSlot(0); // hoe
            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(tile), "till");
            t.Assert(!service.Current.HasFlag(StoryKeys.FirstPlanting),
                "tilling alone does not stamp the planting flag");

            WorldSim.Instance.SelectSlot(3); // turnip seeds
            t.AssertEqual(ActionOutcome.Planted, WorldSim.Instance.UseSelectedItem(tile), "plant");
            t.Assert(service.Current.HasFlag(StoryKeys.FirstPlanting), "first plant stamps the flag");
            t.AssertEqual(Clock.Instance.Now.DayIndex, service.Current.FlagDay(StoryKeys.FirstPlanting),
                "flag stamped with today's day-index");

            // A later-day second planting must not restamp (only-if-absent).
            Clock.Instance.AdvanceToDayStart();
            var second = new Vector2I(21, 14);
            WorldSim.Instance.SelectSlot(0);
            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(second), "second till");
            WorldSim.Instance.SelectSlot(3);
            t.AssertEqual(ActionOutcome.Planted, WorldSim.Instance.UseSelectedItem(second), "second plant");
            t.AssertEqual(0L, service.Current.FlagDay(StoryKeys.FirstPlanting),
                "second plant on day 1 leaves the day-0 stamp unchanged");
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
    public static async Task Story_QuitMidBeatRetriggers(TestContext t)
    {
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Plant on day 0 so the road clears at dawn 1 and the crew beat pends.
            var tile = new Vector2I(20, 14);
            WorldSim.Instance.SelectSlot(0); // hoe
            t.AssertEqual(ActionOutcome.Tilled, WorldSim.Instance.UseSelectedItem(tile), "till");
            WorldSim.Instance.SelectSlot(3); // turnip seeds
            t.AssertEqual(ActionOutcome.Planted, WorldSim.Instance.UseSelectedItem(tile), "plant");

            // Sleep into the crew morning. The beat fires straight out of the sleep flow's
            // return to Playing, so wait for the day advance + the beat — never for Playing.
            long dayBefore = Clock.Instance.Now.DayIndex;
            GameState.Instance.TransitionTo(GameState.Phase.Sleeping);
            t.Assert(await t.WaitUntil(() => Clock.Instance.Now.DayIndex > dayBefore, 10),
                "sleep advanced the day");
            t.Assert(await t.WaitUntil(() => WorldSim.Instance.ActiveDialogue != null, 10),
                "crew beat started after the planted sleep");
            t.AssertEqual("intro_crew_arrival", WorldSim.Instance.ActiveDialogue!.Def.Id,
                "the active dialogue is the crew arrival beat");

            // "Quit mid-beat": the last save on disk is the morning autosave — arrival
            // flags in, completion flag absent (dialogue terminals stamp it, and none ran).
            string autosavePath = Path.Combine(
                SaveService.SaveDirectory, SaveService.DefaultSlot + ".json");
            t.Assert(File.Exists(autosavePath), "morning autosave exists");
            string autosaveJson = File.ReadAllText(autosavePath);
            JsonNode saved = JsonNode.Parse(autosaveJson)!;
            t.Assert(saved["StoryFlags"]?[StoryKeys.RoadCleared] != null,
                "autosave contains intro.road_cleared");
            t.Assert(saved["StoryFlags"]?[StoryKeys.CrewArrivalDone] == null,
                "autosave lacks the completion flag");

            // Reload mid-beat: director aborts via AfterLoad, WorldSim nulls the session,
            // and the beat re-derives as pending from the model alone.
            SaveService.Instance.DeserializeFrom(autosaveJson);
            t.Assert(WorldSim.Instance.ActiveDialogue == null,
                "mid-beat reload cleared the dialogue session");
            t.AssertEqual((StoryBeatId?)StoryBeatId.CrewArrival,
                IntroRules.PendingBeat(SaveService.Instance.Current, Clock.Instance.Now,
                    SaveService.Instance.Current.Player.MapId),
                "PendingBeat re-derives CrewArrival from the reloaded save");

            // The director replays the beat from the top; complete it so cleanup never
            // frees Main mid-beat.
            t.Assert(await t.WaitUntil(() => WorldSim.Instance.ActiveDialogue != null, 10),
                "crew beat re-triggered after the reload");
            await DriveDialogueToCompletion(t, "replayed crew beat");
            t.Assert(SaveService.Instance.Current.HasFlag(StoryKeys.CrewArrivalDone),
                "completion flag stamped by the replayed beat");
            t.Assert(await t.WaitUntil(
                () => GameState.Instance.Current == GameState.Phase.Playing, 10),
                "phase restored to Playing after the beat");
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

    private static void ApplyDawn(GameData data, long newDay)
    {
        foreach (string flag in IntroRules.FlagsToSetOnDayStarted(data, newDay))
        {
            data.TrySetFlag(flag, newDay);
        }
    }

    // Drives the active dialogue to completion from the outside, one pump per frame.
    // Choices are picked round-robin per node, so hub-and-spoke graphs (the town
    // meeting) reach their exit choice wherever the copy puts it.
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
