using Godot;
using TheHaunt.Core;
using TheHaunt.Player;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class TravelTests
{
    // Documented spawn roster per map (spec §2.6) — each must resolve to a real
    // Marker2D, never the camera-center fallback.
    private static readonly Dictionary<string, string[]> DocumentedSpawns = new()
    {
        [MapIds.Farm] = new[] { "default", "road" },
        [MapIds.Town] = new[] { "from_farm", "from_hall" },
        [MapIds.TownHall] = new[] { "entry" },
    };

    [SimTest]
    public static async Task Map_RegistryCreatesAll(TestContext t)
    {
        SaveService service = SaveService.Instance;
        MapRoot? map = null;
        try
        {
            service.NewGame();
            t.Assert(!MapRegistry.Contains("no_such_map"), "unknown id not in the registry");

            foreach (string id in MapIds.All)
            {
                t.Assert(MapRegistry.Contains(id), $"registry contains '{id}'");
                map = MapRegistry.Create(id);
                t.Host.AddChild(map);
                await t.WaitFrames(1);

                t.AssertEqual(id, map.MapId, $"'{id}': MapId set on the created map");
                foreach (string spawn in DocumentedSpawns[id])
                {
                    t.Assert(map.GetNodeOrNull<Marker2D>($"Spawns/{spawn}") != null,
                        $"'{id}': spawn '{spawn}' is a real marker, not the fallback");
                }
                Rect2 limits = map.GetCameraLimits();
                t.Assert(limits.Size.X >= 640 && limits.Size.Y >= 360,
                    $"'{id}': camera limits {limits.Size} cover at least the 640x360 viewport");

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
    public static async Task Map_ExitDisabledWhileBlocked(TestContext t)
    {
        SaveService service = SaveService.Instance;
        TestMap? map = null;
        PlayerController? player = null;
        int requests = 0;
        Action<string, string> onTravel = (_, _) => requests++;
        WorldSim.Instance.TravelRequested += onTravel;
        try
        {
            service.NewGame(); // fresh game: no RoadCleared flag
            map = new TestMap { MapId = MapIds.Farm };
            t.Host.AddChild(map);
            await t.WaitFrames(1);
            map.ApplyState(service.Current.GetMap(MapIds.Farm)); // hydrate like Main.LoadMap
            await t.WaitFrames(2); // let the deferred Monitoring toggle land

            var obstacles = map.GetNodeOrNull<TileMapLayer>("Obstacles");
            t.Assert(obstacles != null, "Obstacles layer exists");
            foreach (var cell in new[]
            {
                new Vector2I(36, 14), new Vector2I(36, 15), new Vector2I(37, 14), new Vector2I(37, 15),
            })
            {
                t.Assert(obstacles!.GetCellSourceId(cell) != -1,
                    $"debris cell {cell} present while the road is blocked");
            }

            MapExit? exit = FindExit(map, MapIds.Town);
            t.Assert(exit != null, "road MapExit exists on the farm");
            t.Assert(!(exit!.IsEnabled?.Invoke() ?? true), "road exit IsEnabled reports blocked");
            t.Assert(!exit.Monitoring, "road exit monitoring disabled while blocked");

            // Belt AND suspenders: even a player standing inside the exit area must not
            // trigger a transition while the road is blocked.
            player = new PlayerController();
            t.Host.AddChild(player);
            player.GlobalPosition = new Vector2(38 * 16 + 8, 15 * 16 + 8); // exit tile center
            await t.WaitFrames(10);
            t.AssertEqual(0, requests, "no TravelRequested while the road is blocked");
            t.AssertEqual(MapIds.Farm, service.Current.Player.MapId, "player MapId unchanged");
        }
        finally
        {
            WorldSim.Instance.TravelRequested -= onTravel;
            if (player != null && GodotObject.IsInstanceValid(player))
            {
                player.Free();
            }
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
    public static async Task Travel_SwapsMapAndModel(TestContext t)
    {
        Node? main = null;
        int requests = 0;
        Action<string, string> onTravel = (_, _) => requests++;
        WorldSim.Instance.TravelRequested += onTravel;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Keep the story quiet: both completion flags, back-to-back so no beat can
            // slip in between the deferred trigger checks.
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            var player = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(player != null, "World/Player exists after boot");

            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Town, "from_farm"),
                "travel request to town accepted");
            bool arrived = await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.Town
                    && GameState.Instance.Current == GameState.Phase.Playing,
                10);
            t.Assert(arrived, "travel completed: model MapId is town, phase back to Playing");
            t.AssertEqual(1, requests, "exactly one TravelRequested fired");
            t.Assert(WorldSim.Instance.IsMapActive(MapIds.Town), "town map active after travel");
            t.Assert(!WorldSim.Instance.IsMapActive(MapIds.Farm), "farm map no longer active");

            MapRoot? town = FindCurrentMap(main);
            t.Assert(town != null, "town map instanced under MapHost");
            t.AssertEqual(town!.GetSpawn("from_farm"), player!.GlobalPosition,
                "player placed at the arrival spawn");

            // Spawn-clearance rule: the arrival spot must not overlap the town's west
            // exit, so no second transition may fire on its own.
            await t.WaitFrames(30);
            t.AssertEqual(1, requests, "no spurious second transition after arrival");
            t.AssertEqual(MapIds.Town, SaveService.Instance.Current.Player.MapId,
                "still in town after settling");

            // And back again.
            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Farm, "road"),
                "travel request back to the farm accepted");
            bool returned = await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.Farm
                    && GameState.Instance.Current == GameState.Phase.Playing,
                10);
            t.Assert(returned, "return travel completed");
            t.AssertEqual(2, requests, "exactly two TravelRequested fired in total");
            t.Assert(WorldSim.Instance.IsMapActive(MapIds.Farm), "farm map active again");
            t.Assert(!WorldSim.Instance.IsMapActive(MapIds.Town), "town map gone again");

            await t.WaitFrames(30);
            t.AssertEqual(2, requests, "no spurious transition after returning");
            t.Assert(!SaveService.Instance.SaveFileExists(),
                "no autosave written by travel (sleep is the only autosave)");
        }
        finally
        {
            WorldSim.Instance.TravelRequested -= onTravel;
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Travel_RefusedWithoutControl(TestContext t)
    {
        SaveService service = SaveService.Instance;
        int requests = 0;
        Action<string, string> onTravel = (_, _) => requests++;
        WorldSim.Instance.TravelRequested += onTravel;
        try
        {
            service.NewGame();
            t.Assert(!WorldSim.Instance.RequestTravel("no_such_map", "default"),
                "unknown map id refused");
            t.AssertEqual(0, requests, "no event for the unknown map");

            t.Assert(WorldSim.Instance.StartDialogue("foreman_wait"), "dialogue started");
            t.Assert(!GameState.Instance.PlayerHasControl, "no control during dialogue");
            t.Assert(!WorldSim.Instance.RequestTravel(MapIds.Town, "from_farm"),
                "travel refused during dialogue");
            t.AssertEqual(0, requests, "no TravelRequested during dialogue");

            // Finish the dialogue; control (and travel) come back.
            for (int step = 0; step < 100 && WorldSim.Instance.ActiveDialogue != null; step++)
            {
                DialogueSession session = WorldSim.Instance.ActiveDialogue;
                if (session.AtChoices)
                {
                    WorldSim.Instance.ChooseDialogueOption(0);
                }
                else
                {
                    WorldSim.Instance.AdvanceDialogue();
                }
                await t.WaitFrames(1);
            }
            t.Assert(WorldSim.Instance.ActiveDialogue == null, "dialogue ran to completion");
            t.Assert(await t.WaitUntil(() => GameState.Instance.PlayerHasControl, 5),
                "control restored after the dialogue");

            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Town, "from_farm"),
                "travel accepted after the dialogue");
            t.AssertEqual(1, requests, "TravelRequested fired once control returned");
        }
        finally
        {
            WorldSim.Instance.TravelRequested -= onTravel;
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    // Depth-first search for a MapExit targeting the given map — the tests must not
    // depend on where in the map's tree the exit is parented.
    private static MapExit? FindExit(Node root, string targetMapId)
    {
        if (root is MapExit exit && exit.TargetMapId == targetMapId)
        {
            return exit;
        }
        foreach (Node child in root.GetChildren())
        {
            if (FindExit(child, targetMapId) is { } found)
            {
                return found;
            }
        }
        return null;
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
