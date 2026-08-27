using Godot;
using TheHaunt.Core;
using TheHaunt.Player;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// The road-strip cast (docs/story/cast.md): every scheduled staging tile must be
/// ground an NPC can actually stand on, and every dialogue id the selector can hand
/// out must resolve. Both were previously unguarded — a placement inside a counter
/// blocker or a selector arm pointing at a missing def fails silently in play
/// (the NPC spawns in furniture; the Talk prompt shows and the press does nothing).
/// </summary>
public static class NpcStagingTests
{
    [SimTest]
    public static async Task Npc_StagingTilesAreStandable(TestContext t)
    {
        SaveService service = SaveService.Instance;
        MapRoot? map = null;
        try
        {
            service.NewGame();

            // Every (map, tile) pair any schedule can ever produce, deduplicated.
            var byMap = new Dictionary<string, HashSet<Vector2I>>();
            foreach (NpcDef def in NpcDefs.All.Values)
            {
                foreach (ScheduleEntry entry in def.Schedule)
                {
                    t.Assert(MapRegistry.Contains(entry.Placement.MapId),
                        $"'{def.Id}' stages on registered map '{entry.Placement.MapId}'");
                    if (!byMap.TryGetValue(entry.Placement.MapId, out HashSet<Vector2I>? tiles))
                    {
                        tiles = new HashSet<Vector2I>();
                        byMap[entry.Placement.MapId] = tiles;
                    }
                    tiles.Add(new Vector2I(entry.Placement.TileX, entry.Placement.TileY));
                }
            }
            t.Assert(byMap.Count >= 6, $"the cast spreads over the world ({byMap.Count} maps)");

            foreach ((string mapId, HashSet<Vector2I> tiles) in byMap)
            {
                map = MapRegistry.Create(mapId);
                t.Host.AddChild(map);
                await t.WaitFrames(1);
                // Hydrate like Main.LoadMap — the farm's staging shares a map with
                // the storm debris, and the debris only paints on ApplyState.
                map.ApplyState(service.Current.GetMap(mapId));
                await t.WaitFrames(1);

                foreach (Vector2I tile in tiles)
                {
                    t.Assert(map.IsStandable(tile),
                        $"staging tile {tile} on '{mapId}' is standable ground");
                }

                map.Free();
                map = null;
                await t.WaitFrames(1);
            }
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
    public static void Npc_AmbientSelectorResolves(TestContext t)
    {
        // Sweep: whatever the selector returns for any role, at any sampled hour, in
        // any intro flag state, must be a def that exists. A dangling id renders a
        // Talk prompt whose press does nothing.
        var bare = new GameData();
        var afterMeeting = new GameData();
        afterMeeting.TrySetFlag(StoryKeys.CrewArrivalDone, 0);
        afterMeeting.TrySetFlag(StoryKeys.MeetingDone, 0);
        int[] minutes = { 0, 100, 180, 240, 479, 480, 540, 659, 660, 700, 840, 1100, 1199 };

        foreach (string roleId in NpcDefs.All.Keys)
        {
            foreach (GameData data in new[] { bare, afterMeeting })
            {
                foreach (int minute in minutes)
                {
                    foreach (long day in new long[] { 0, 1 })
                    {
                        string? id = DialogueSelector.ForNpc(
                            roleId, data, new GameTime(day * GameTime.MinutesPerDay + minute));
                        if (id != null)
                        {
                            t.Assert(DialogueDefs.TryGet(id) != null,
                                $"'{roleId}' at minute {minute} (day {day}): '{id}' resolves");
                        }
                    }
                }
            }
        }

        // Walt's canon clock: quiet before 2 PM, the good hours 2-5, June after.
        t.AssertEqual("walt_morning", DialogueSelector.ForNpc("walt", bare, new GameTime(479)),
            "Walt is quiet up to 2 PM");
        t.AssertEqual("walt_sharp", DialogueSelector.ForNpc("walt", bare, new GameTime(480)),
            "the good hours open at 2 PM");
        t.AssertEqual("walt_sharp", DialogueSelector.ForNpc("walt", bare, new GameTime(659)),
            "and run to 5 PM");
        t.AssertEqual("walt_low", DialogueSelector.ForNpc("walt", bare, new GameTime(660)),
            "after 5 it's June and the ledger");

        // The guarded locals speak more freely once the meeting has happened.
        foreach ((string role, string before, string after) in new[]
        {
            ("gloria", "gloria_before", "gloria_after"),
            ("billie", "billie_before", "billie_after"),
            ("harriet", "harriet_before", "harriet_after"),
            ("abe", "abe_before", "abe_after"),
        })
        {
            t.AssertEqual(before, DialogueSelector.ForNpc(role, bare, new GameTime(300)),
                $"'{role}' holds back before the meeting");
            t.AssertEqual(after, DialogueSelector.ForNpc(role, afterMeeting, new GameTime(300)),
                $"'{role}' opens up after it");
        }

        // The fixtures alternate by day so two visits in a row are never identical.
        foreach ((string role, string even, string odd) in new[]
        {
            ("sam", "sam_a", "sam_b"), ("bud", "bud_a", "bud_b"), ("dennis", "dennis_a", "dennis_b"),
        })
        {
            t.AssertEqual(even, DialogueSelector.ForNpc(role, bare, new GameTime(300)),
                $"'{role}' day-0 line");
            t.AssertEqual(odd, DialogueSelector.ForNpc(
                role, bare, new GameTime(GameTime.MinutesPerDay + 300)), $"'{role}' day-1 line");
        }
    }

    [SimTest]
    public static async Task Npc_TalkPathReachesTheBarkeep(TestContext t)
    {
        // The whole talk loop, end to end, for the road cast: clock stages the view,
        // the probe focuses it, the press opens the selector's dialogue. Every link
        // is covered pure elsewhere; this is the only place the composition runs —
        // a regression in the talk collider, CanInteract, or the probe constants
        // would otherwise leave all fourteen road NPCs silently untalkable.
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Keep the story quiet: both completion flags, back-to-back so no beat
            // can slip in between the deferred trigger checks.
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            var maybePlayer = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(maybePlayer != null, "World/Player exists after boot");
            PlayerController player = maybePlayer!;

            t.Assert(WorldSim.Instance.RequestTravel(MapIds.BilliesBar, "entry"),
                "travel into the bar accepted");
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.BilliesBar
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "standing in the bar");

            // 6:00 AM -> 10:00 AM: the ticks stage the open-for-business cast.
            Clock.Instance.AdvanceMinutes(240);
            await t.WaitFrames(2);

            MapRoot? bar = FindCurrentMap(main);
            t.Assert(bar != null, "bar instanced under MapHost");
            NpcView? billie = bar!.GetNpcView("billie");
            t.Assert(billie != null, "the barkeep staged at opening time");
            t.AssertEqual(new Vector2(2 * 16 + 8, 4 * 16 + 8), billie!.Position,
                "at the west end of the counter");

            // One tile below, facing up: the probe's circle laps the talk area.
            player.GlobalPosition = billie.GlobalPosition + new Vector2(0, 16);
            player.Probe.SetFacing(3);
            t.Assert(await t.WaitUntil(
                () => player.Probe.Focused is NpcView { RoleId: "billie" }, 2),
                "probe focused the barkeep");
            t.AssertEqual("Talk", player.Probe.Focused!.PromptText, "focused prompt text");

            player.Probe.TryInteract(player);
            t.Assert(WorldSim.Instance.ActiveDialogue != null, "the press opened a session");
            t.AssertEqual("billie_after", WorldSim.Instance.ActiveDialogue!.Def.Id,
                "on the selector's post-meeting line");

            // Run it out so the next test starts from a clean phase.
            for (int step = 0; step < 20 && WorldSim.Instance.ActiveDialogue != null; step++)
            {
                WorldSim.Instance.AdvanceDialogue();
                await t.WaitFrames(1);
            }
            t.Assert(WorldSim.Instance.ActiveDialogue == null, "dialogue ran to completion");
        }
        finally
        {
            await CleanupMainAsync(t, main);
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
