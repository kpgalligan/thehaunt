using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class NpcTests
{
    [SimTest]
    public static void Npc_ScheduleResolveDeterministic(TestContext t)
    {
        // Boundary + priority semantics pinned on a locally built def, so the assertions
        // survive any re-staging of the shipped table: entries are start-inclusive /
        // end-exclusive, and the FIRST passing entry wins.
        var overlapping = new NpcDef("test_npc", "Test", CharacterSprites.SheetPath, 0, new[]
        {
            new ScheduleEntry(null, null, 120, 600, new NpcPlacement(MapIds.Town, 1, 1, 0)),
            new ScheduleEntry(null, null, 0, 1200, new NpcPlacement(MapIds.Farm, 2, 2, 1)),
        });
        var data = new GameData();

        NpcPlacement? At(int minute) =>
            NpcSchedules.Resolve(overlapping, data, new GameTime(3 * GameTime.MinutesPerDay + minute));

        t.AssertEqual(new NpcPlacement(MapIds.Farm, 2, 2, 1), At(0)!.Value,
            "minute 0: only the all-day entry matches");
        t.AssertEqual(new NpcPlacement(MapIds.Town, 1, 1, 0), At(120)!.Value,
            "start minute is inclusive");
        t.AssertEqual(new NpcPlacement(MapIds.Town, 1, 1, 0), At(300)!.Value,
            "overlap: first matching entry wins");
        t.AssertEqual(new NpcPlacement(MapIds.Farm, 2, 2, 1), At(600)!.Value,
            "end minute is exclusive — falls through to the next entry");
        t.AssertEqual(new NpcPlacement(MapIds.Farm, 2, 2, 1), At(1199)!.Value,
            "last minute of the day matches the all-day entry");

        // Deterministic: same (flags, time) resolves bit-equal twice.
        t.AssertEqual(At(300), At(300), "same inputs resolve identically");

        // Flag gates: RequiresFlag absent or ForbidsFlag present skips the entry.
        var gated = new NpcDef("test_gated", "Test", CharacterSprites.SheetPath, 0, new[]
        {
            new ScheduleEntry("test.required", "test.forbidden", 0, 1200,
                new NpcPlacement(MapIds.TownHall, 3, 3, 2)),
        });
        var noFlags = new GameData();
        t.Assert(NpcSchedules.Resolve(gated, noFlags, new GameTime(100)) == null,
            "RequiresFlag absent: entry skipped, npc absent");
        var hasRequired = new GameData();
        hasRequired.TrySetFlag("test.required", 0);
        t.AssertEqual(new NpcPlacement(MapIds.TownHall, 3, 3, 2),
            NpcSchedules.Resolve(gated, hasRequired, new GameTime(100))!.Value,
            "RequiresFlag present: entry matches");
        hasRequired.TrySetFlag("test.forbidden", 0);
        t.Assert(NpcSchedules.Resolve(gated, hasRequired, new GameTime(100)) == null,
            "ForbidsFlag present: entry skipped, npc absent");

        // No matching window at all: null = absent.
        var windowed = new NpcDef("test_window", "Test", CharacterSprites.SheetPath, 0, new[]
        {
            new ScheduleEntry(null, null, 500, 600, new NpcPlacement(MapIds.Farm, 4, 4, 3)),
        });
        t.Assert(NpcSchedules.Resolve(windowed, new GameData(), new GameTime(499)) == null,
            "before the only window: absent");

        // Shipped table sanity: every placement targets a registered map id.
        foreach (NpcDef def in NpcDefs.All.Values)
        {
            foreach (ScheduleEntry entry in def.Schedule)
            {
                t.Assert(MapIds.All.Contains(entry.Placement.MapId),
                    $"npc '{def.Id}': placement map '{entry.Placement.MapId}' is in MapIds.All");
            }
        }
    }

    [SimTest]
    public static void Npc_IntroStaging(TestContext t)
    {
        string[] crewRoles = { "foreman", "crew_worker_a", "crew_worker_b" };
        int[] sampleMinutes = { 0, 300, 719, 720, 900, 1199 };

        static GameTime Day3(int minute) => new(3 * GameTime.MinutesPerDay + minute);

        // No flags: nobody stages on the farm (the road is still blocked).
        var untouched = new GameData();
        foreach (string role in crewRoles)
        {
            foreach (int minute in sampleMinutes)
            {
                NpcPlacement? placed = NpcSchedules.Resolve(NpcDefs.All[role], untouched, Day3(minute));
                t.Assert(placed == null || placed.Value.MapId != MapIds.Farm,
                    $"no flags: '{role}' not on the farm at minute {minute}");
            }
        }

        // Road cleared, arrival pending: the whole crew stages on the farm all day —
        // flag-bounded, not clock-bounded, so the beat can never be stranded castless.
        var crewPending = new GameData();
        crewPending.TrySetFlag(StoryKeys.FirstPlanting, 1);
        crewPending.TrySetFlag(StoryKeys.RoadCleared, 2);
        foreach (int minute in sampleMinutes)
        {
            t.AssertEqual(new NpcPlacement(MapIds.Farm, 33, 15, 1),
                NpcSchedules.Resolve(NpcDefs.All["foreman"], crewPending, Day3(minute))!.Value,
                $"crew pending: foreman at the road mouth at minute {minute}");
            t.AssertEqual(new NpcPlacement(MapIds.Farm, 34, 14, 1),
                NpcSchedules.Resolve(NpcDefs.All["crew_worker_a"], crewPending, Day3(minute))!.Value,
                $"crew pending: worker a staged at minute {minute}");
            t.AssertEqual(new NpcPlacement(MapIds.Farm, 32, 16, 3),
                NpcSchedules.Resolve(NpcDefs.All["crew_worker_b"], crewPending, Day3(minute))!.Value,
                $"crew pending: worker b staged at minute {minute}");
        }

        // Meeting pending: the mayor takes the podium every pending evening, in-window
        // only — that recurrence is the free missed-meeting recovery.
        var meetingPending = new GameData();
        meetingPending.TrySetFlag(StoryKeys.FirstPlanting, 1);
        meetingPending.TrySetFlag(StoryKeys.RoadCleared, 2);
        meetingPending.TrySetFlag(StoryKeys.CrewArrivalDone, 2);
        foreach (int minute in new[] { 720, 900, 1199 })
        {
            t.AssertEqual(new NpcPlacement(MapIds.TownHall, 20, 6, 0),
                NpcSchedules.Resolve(NpcDefs.All["mayor"], meetingPending, Day3(minute))!.Value,
                $"meeting pending: mayor at the podium at minute {minute}");
        }
        NpcPlacement? beforeWindow =
            NpcSchedules.Resolve(NpcDefs.All["mayor"], meetingPending, Day3(719));
        t.Assert(beforeWindow == null
            || beforeWindow.Value != new NpcPlacement(MapIds.TownHall, 20, 6, 0),
            "meeting pending: mayor not at the podium before 18:00");

        // After crew_done nobody stages on the farm — completed states never re-stage.
        var afterMeeting = new GameData();
        afterMeeting.TrySetFlag(StoryKeys.FirstPlanting, 1);
        afterMeeting.TrySetFlag(StoryKeys.RoadCleared, 2);
        afterMeeting.TrySetFlag(StoryKeys.CrewArrivalDone, 2);
        foreach (GameData state in new[] { meetingPending, afterMeeting })
        {
            foreach (NpcDef def in NpcDefs.All.Values)
            {
                foreach (int minute in sampleMinutes)
                {
                    NpcPlacement? placed = NpcSchedules.Resolve(def, state, Day3(minute));
                    t.Assert(placed == null || placed.Value.MapId != MapIds.Farm,
                        $"crew done: '{def.Id}' not on the farm at minute {minute}");
                }
            }
        }
        afterMeeting.TrySetFlag(StoryKeys.MeetingDone, 2);
        t.Assert(NpcSchedules.Resolve(NpcDefs.All["mayor"], afterMeeting, Day3(900)) == null
            || NpcSchedules.Resolve(NpcDefs.All["mayor"], afterMeeting, Day3(900))!.Value
                != new NpcPlacement(MapIds.TownHall, 20, 6, 0),
            "meeting done: mayor leaves the podium");
    }

    [SimTest]
    public static void Npc_ShopkeeperSchedule(TestContext t)
    {
        // The shopkeeper keeps shop hours and nothing else: behind the counter of the
        // general store during 180-659, absent otherwise, invariant under every
        // intro-flag combination — and never staged on farm/town/town_hall, so the
        // intro staging is untouched.
        NpcDef shopkeeper = NpcDefs.All["shopkeeper"];
        var behindCounter = new NpcPlacement(MapIds.GeneralStore, 6, 3, 0, Ambit: 1);
        string[] introFlags =
        {
            StoryKeys.FirstPlanting, StoryKeys.RoadCleared,
            StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
        };
        int[] minutes = { 0, 179, 180, 400, 659, 660, 1199 };

        for (int combo = 0; combo < 16; combo++)
        {
            var data = new GameData();
            for (int bit = 0; bit < introFlags.Length; bit++)
            {
                if ((combo & (1 << bit)) != 0)
                {
                    data.TrySetFlag(introFlags[bit], 1);
                }
            }
            foreach (int minute in minutes)
            {
                NpcPlacement? placed = NpcSchedules.Resolve(
                    shopkeeper, data, new GameTime(3 * GameTime.MinutesPerDay + minute));
                if (minute >= 180 && minute < 660)
                {
                    t.Assert(placed != null, $"combo {combo}: present at minute {minute}");
                    t.AssertEqual(behindCounter, placed!.Value,
                        $"combo {combo}: behind the counter at minute {minute}");
                }
                else
                {
                    t.Assert(placed == null, $"combo {combo}: absent at minute {minute}");
                }
                t.Assert(placed == null || placed.Value.MapId == MapIds.GeneralStore,
                    $"combo {combo}: never outside the store at minute {minute}");
            }
        }
    }

    [SimTest]
    public static async Task Npc_ViewSpawnDespawn(TestContext t)
    {
        SaveService service = SaveService.Instance;
        TestMap? map = null;
        try
        {
            service.NewGame(); // day 0, minute 0, no flags
            map = new TestMap { MapId = MapIds.Farm };
            t.Host.AddChild(map);
            await t.WaitFrames(1);
            t.Assert(map.GetNpcView("foreman") == null, "no crew before the road clears");

            // SetStoryFlag syncs NPCs synchronously on a new set.
            t.Assert(WorldSim.Instance.SetStoryFlag(StoryKeys.RoadCleared), "stamp road_cleared");
            NpcView? foreman = map.GetNpcView("foreman");
            t.Assert(foreman != null, "foreman view spawned after the flag sync");
            t.AssertEqual(new Vector2(33 * 16 + 8, 15 * 16 + 8), foreman!.GlobalPosition,
                "foreman at the scheduled tile center (road mouth)");
            t.AssertEqual("Talk", foreman.PromptText, "npc prompt text");
            t.Assert(map.GetNpcView("crew_worker_a") != null, "worker a view spawned");
            t.AssertEqual(new Vector2(34 * 16 + 8, 14 * 16 + 8),
                map.GetNpcView("crew_worker_a")!.GlobalPosition, "worker a tile center");
            t.Assert(map.GetNpcView("crew_worker_b") != null, "worker b view spawned");
            t.AssertEqual(new Vector2(32 * 16 + 8, 16 * 16 + 8),
                map.GetNpcView("crew_worker_b")!.GlobalPosition, "worker b tile center");

            // Departure: removed from the lookup immediately, node freed at end of frame.
            t.Assert(WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone), "stamp crew_done");
            t.Assert(map.GetNpcView("foreman") == null,
                "departed view removed from the lookup immediately");
            await t.WaitFrames(1);
            t.Assert(!GodotObject.IsInstanceValid(foreman), "foreman node freed after one frame");
            t.Assert(map.GetNpcView("crew_worker_a") == null, "worker a gone");
            t.Assert(map.GetNpcView("crew_worker_b") == null, "worker b gone");
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
}
