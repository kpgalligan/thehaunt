using Godot;
using TheHaunt.Core;
using TheHaunt.Player;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class TravelTests
{
    // Documented spawn roster per map (phase3-spec §2.6 + phase3b-spec §4) — each
    // must resolve to a real Marker2D, never the camera-center fallback.
    private static readonly Dictionary<string, string[]> DocumentedSpawns = new()
    {
        [MapIds.Farm] = new[] { "default", "road", "house_door", "barn_door" },
        [MapIds.Town] = new[] { "from_fork", "from_east_fork", "from_hall", "from_store" },
        [MapIds.TownHall] = new[] { "entry" },
        [MapIds.FarmHouse] = new[] { "default", "entry" },
        [MapIds.GeneralStore] = new[] { "default", "entry" },
        [MapIds.Barn] = new[] { "default", "entry" },
        [MapIds.WestEntry] = new[] { "default", RoadWrap.ArrivalSpawn, "from_billies" },
        [MapIds.Billies] = new[] { "default", "from_west_entry", "from_fork" },
        [MapIds.Fork] = new[] { "default", "from_billies", "from_town", "from_farm" },
        [MapIds.EastFork] = new[] { "default", "from_town", "from_east_entry" },
        [MapIds.EastEntry] = new[] { "default", "from_east_fork", RoadWrap.ArrivalSpawn },
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
                // Set equality, not just containment: a marker added to a map without
                // a roster entry would otherwise drift from the documentation unpinned.
                t.AssertEqual(DocumentedSpawns[id].Length,
                    map.GetNodeOrNull("Spawns")?.GetChildCount() ?? 0,
                    $"'{id}': every marker on the map is in the documented roster");
                Rect2 limits = map.GetCameraLimits();
                t.Assert(limits.Size.X >= MapRoot.ViewportWidth && limits.Size.Y >= MapRoot.ViewportHeight,
                    $"'{id}': camera limits {limits.Size} cover at least the "
                    + $"{MapRoot.ViewportWidth}x{MapRoot.ViewportHeight} viewport");

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
        (string MapId, string SpawnId) lastRequest = ("", "");
        Action<string, string> onTravel = (mapId, spawnId) => { requests++; lastRequest = (mapId, spawnId); };
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

            MapExit? exit = FindExit(map, MapIds.Fork);
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

            // And the other half of the gate: the crew clears the road and the same
            // exit wakes up. SetStoryFlag repaints every registered map, this one
            // included, so the debris and the Monitoring toggle both flip here.
            WorldSim.Instance.SetStoryFlag(StoryKeys.RoadCleared);
            await t.WaitFrames(2); // let the deferred Monitoring toggle land
            t.Assert(exit.IsEnabled?.Invoke() ?? false, "road exit IsEnabled reports cleared");
            t.Assert(exit.Monitoring, "road exit monitoring re-enabled once cleared");

            // Step off and back on: the cleared exit fires, and it leads to the fork.
            player.GlobalPosition = new Vector2(20 * 16 + 8, 15 * 16 + 8);
            await t.WaitFrames(2);
            player.GlobalPosition = new Vector2(38 * 16 + 8, 15 * 16 + 8);
            t.Assert(await t.WaitUntil(() => requests > 0, 5), "cleared exit fires TravelRequested");
            t.AssertEqual((MapIds.Fork, "from_farm"), lastRequest,
                "and it leads to the fork's farm-road arrival");
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

            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Town, "from_fork"),
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
            t.AssertEqual(town!.GetSpawn("from_fork"), player!.GlobalPosition,
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
            t.Assert(!WorldSim.Instance.RequestTravel(MapIds.Town, "from_fork"),
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

            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Town, "from_fork"),
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

    [SimTest]
    public static async Task Travel_InteriorRoundTrips(TestContext t)
    {
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Keep the story quiet while we tour the interiors (back-to-back so no
            // beat can slip in between the deferred trigger checks).
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            var maybePlayer = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(maybePlayer != null, "World/Player exists after boot");
            PlayerController player = maybePlayer!;

            // Farm -> farmhouse through the actual HouseDoor node (its target wiring
            // is under test, not just the travel bus).
            await EnterDoor(t, main, player, MapIds.FarmHouse, "farm door into the farmhouse");
            // Interior 'entry' spawns sit one tile above their door, so the player's
            // feet box laps the door blocker by 2 px and MoveAndSlide may nudge the
            // body out — a tight radius still cleanly rules out the fallback spawn.
            AssertNearSpawn(t, player, new Vector2(120, 136),
                "arrived at the farmhouse 'entry' spawn, tile (7,8)");

            // Farmhouse -> farm through the interior door: lands at 'house_door'.
            await EnterDoor(t, main, player, MapIds.Farm, "farmhouse door back to the farm");
            t.AssertEqual(new Vector2(120, 136), player.GlobalPosition,
                "arrived at the farm 'house_door' spawn, tile (7,8)");

            // The barn across the yard is the same round trip on the same farm.
            await EnterDoor(t, main, player, MapIds.Barn, "yard door into the barn");
            AssertNearSpawn(t, player, new Vector2(136, 168),
                "arrived at the barn 'entry' spawn, tile (8,10)");
            await EnterDoor(t, main, player, MapIds.Farm, "barn door back to the yard");
            t.AssertEqual(new Vector2(440, 168), player.GlobalPosition,
                "arrived at the farm 'barn_door' spawn, tile (27,10)");

            // Over to town on the bus, then through the StoreDoor and back.
            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Town, "from_fork"),
                "travel to town accepted");
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.Town
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "arrived in town");

            await EnterDoor(t, main, player, MapIds.GeneralStore, "store door off the plaza");
            AssertNearSpawn(t, player, new Vector2(120, 136),
                "arrived at the store 'entry' spawn, tile (7,8)");

            await EnterDoor(t, main, player, MapIds.Town, "store door back to town");
            t.AssertEqual(new Vector2(184, 216), player.GlobalPosition,
                "arrived at the town 'from_store' spawn, tile (11,13)");
            t.AssertEqual(MapIds.Town, SaveService.Instance.Current.Player.MapId,
                "model MapId back in town");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    // Finds the Door leading to targetMapId on the current map, interacts with it,
    // and waits out Main's fade/swap travel flow.
    private static async Task EnterDoor(TestContext t, Node main, PlayerController player,
        string targetMapId, string label)
    {
        t.Assert(await t.WaitUntil(() => GameState.Instance.PlayerHasControl, 10),
            $"{label}: control returned before the door press");
        MapRoot? map = FindCurrentMap(main);
        t.Assert(map != null, $"{label}: a current map exists under MapHost");
        Door? door = FindDoor(map!, targetMapId);
        t.Assert(door != null, $"{label}: a Door targeting '{targetMapId}' exists");
        t.Assert(door!.CanInteract(player), $"{label}: door reports interactable");
        door.Interact(player);
        t.Assert(await t.WaitUntil(
            () => SaveService.Instance.Current.Player.MapId == targetMapId
                && GameState.Instance.Current == GameState.Phase.Playing, 10),
            $"{label}: arrived and returned to Playing");
    }

    // Exactness up to the door-blocker depenetration nudge (see the call sites):
    // 3 px distinguishes the real marker from any fallback by two orders of magnitude.
    private static void AssertNearSpawn(TestContext t, PlayerController player,
        Vector2 expected, string label)
    {
        t.Assert(player.GlobalPosition.DistanceTo(expected) <= 3f,
            $"{label} (expected ~{expected}, got {player.GlobalPosition})");
    }

    // Same DFS as FindExit, for Doors: tests must not depend on where in the map's
    // tree a door is parented.
    private static Door? FindDoor(Node root, string targetMapId)
    {
        if (root is Door door && door.TargetMapId == targetMapId)
        {
            return door;
        }
        foreach (Node child in root.GetChildren())
        {
            if (FindDoor(child, targetMapId) is { } found)
            {
                return found;
            }
        }
        return null;
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
