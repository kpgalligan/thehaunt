using Godot;
using TheHaunt.Core;
using TheHaunt.Player;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// The road strip (docs/story/README.md) — west_entry, billies, fork, town, east_fork,
/// east_entry, with the farm hanging off the fork's north road — and the town's primary
/// secret: walking out past either entry wraps to the other side. The graph test walks
/// every exit and door on every map and proves its destination spawn is a real marker,
/// because a dangling (map, spawn) pair fails silently in play (GetSpawn falls back to
/// the camera centre) and loudly here.
/// </summary>
public static class RoadWrapTests
{
    [SimTest]
    public static async Task Roads_WrapPairIsSymmetric(TestContext t)
    {
        // Not RoadWrap's constants read back at themselves — the two entry maps'
        // actual outward exit NODES, which a hand edit to either map could silently
        // point somewhere else without touching the rule.
        MapRoot west = MapRegistry.Create(MapIds.WestEntry);
        MapRoot east = MapRegistry.Create(MapIds.EastEntry);
        t.Host.AddChild(west);
        t.Host.AddChild(east);
        await t.WaitFrames(1);
        try
        {
            var westOut = west.GetNodeOrNull<MapExit>("WestExit");
            var eastOut = east.GetNodeOrNull<MapExit>("EastExit");
            t.Assert(westOut != null, "the west entry has its outward west exit");
            t.Assert(eastOut != null, "the east entry has its outward east exit");
            t.AssertEqual(MapIds.EastEntry, westOut!.TargetMapId, "leaving west lands at the east entry");
            t.AssertEqual(MapIds.WestEntry, eastOut!.TargetMapId, "leaving east lands at the west entry");
            t.AssertEqual(RoadWrap.ArrivalSpawn, westOut.TargetSpawnId,
                "arriving on the east entry's wrap marker");
            t.AssertEqual(RoadWrap.ArrivalSpawn, eastOut.TargetSpawnId,
                "arriving on the west entry's wrap marker");
            t.Assert(westOut.IsEnabled == null && eastOut.IsEnabled == null,
                "leaving town is never gated — the wrap is the answer");
        }
        finally
        {
            west.Free();
            east.Free();
            await t.WaitFrames(1);
        }
    }

    [SimTest]
    public static async Task Roads_EveryExitTargetResolves(TestContext t)
    {
        SaveService service = SaveService.Instance;
        MapRoot? map = null;
        try
        {
            service.NewGame();

            // Pass 1: build every map once, recording its spawn markers and every
            // (target map, target spawn) reference its exits and doors carry.
            var spawnsByMap = new Dictionary<string, HashSet<string>>();
            var references = new List<(string FromMap, string Node, string ToMap, string ToSpawn)>();
            foreach (string id in MapIds.All)
            {
                map = MapRegistry.Create(id);
                t.Host.AddChild(map);
                await t.WaitFrames(1);

                var spawns = new HashSet<string>();
                Node? spawnHost = map.GetNodeOrNull("Spawns");
                if (spawnHost != null)
                {
                    foreach (Node marker in spawnHost.GetChildren())
                    {
                        spawns.Add(marker.Name);
                        if (marker is Marker2D point)
                        {
                            // Every marker lands on ground the player can stand on —
                            // an arrival inside a footprint or a chain is a spawn that
                            // works right up until someone widens a building.
                            var tile = new Vector2I(
                                Mathf.FloorToInt(point.GlobalPosition.X / MapRoot.TileSize),
                                Mathf.FloorToInt((point.GlobalPosition.Y + 6) / MapRoot.TileSize));
                            t.Assert(map.IsStandable(tile),
                                $"'{id}': spawn '{marker.Name}' at {tile} is standable");
                        }
                    }
                }
                spawnsByMap[id] = spawns;

                Collect(map, id, references);
                map.Free();
                map = null;
                await t.WaitFrames(1);
            }
            // Exact, re-pinned whenever an exit or door ships — a loose lower bound
            // would let a deleted edge (a whole map unreachable in play) pass silently.
            t.AssertEqual(40, references.Count, "the world graph's full edge count");

            // Pass 2: every reference resolves — a registered target map with a real
            // marker of that name.
            foreach (var (fromMap, node, toMap, toSpawn) in references)
            {
                t.Assert(MapRegistry.Contains(toMap),
                    $"{fromMap}/{node}: target '{toMap}' is a registered map");
                t.Assert(spawnsByMap.TryGetValue(toMap, out HashSet<string>? markers)
                    && markers.Contains(toSpawn),
                    $"{fromMap}/{node}: spawn '{toSpawn}' is a real marker on '{toMap}'");
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
    public static async Task Roads_ClosedRoadsReadAsChains(TestContext t)
    {
        // The chained-off roads and the pit block with something VISIBLE on them — a
        // barrier or the cover, never a bare invisible cell — and the road mouths that
        // are open really are. Signs are found by name: a warning nobody can read is
        // set dressing, not a warning.
        SaveService.Instance.NewGame();

        await CheckFrame(t, MapIds.Fork, map =>
        {
            foreach (int x in new[] { 18, 19, 20, 21 })
                t.Assert(!map.IsStandable(new Vector2I(x, 24)), $"fork south chain blocks at ({x},24)");
            t.Assert(map.IsStandable(new Vector2I(19, 20)), "the south stub is walkable up to the chain");
            t.Assert(map.GetNodeOrNull<RoadBarrier>("SouthChain") != null, "the chain is drawn");
            t.Assert(map.GetNodeOrNull<Sign>("SouthChainSign") != null, "and signed");
            foreach (var mouth in new[] { new Vector2I(0, 15), new Vector2I(39, 15), new Vector2I(19, 0) })
                t.Assert(map.IsStandable(mouth), $"fork road mouth {mouth} is open");
        });

        await CheckFrame(t, MapIds.Billies, map =>
        {
            for (int x = 26; x <= 28; x++)
                for (int y = 20; y <= 21; y++)
                    t.Assert(!map.IsStandable(new Vector2I(x, y)), $"pit cover blocks at ({x},{y})");
            for (int x = 25; x <= 29; x++)
                t.Assert(!map.IsStandable(new Vector2I(x, 19)), $"pit chain blocks at ({x},19)");
            t.Assert(map.GetNodeOrNull<PitCover>("Pit") != null, "the pit cover is drawn");
            t.Assert(map.GetNodeOrNull<Sign>("PitSign") != null, "and warned about");
            t.Assert(!map.IsStandable(new Vector2I(16, 10)), "the bar's footprint blocks");
        });

        await CheckFrame(t, MapIds.EastFork, map =>
        {
            foreach (int x in new[] { 18, 19, 20, 21 })
                t.Assert(!map.IsStandable(new Vector2I(x, 5)), $"mansion chain blocks at ({x},5)");
            t.Assert(map.IsStandable(new Vector2I(19, 10)), "the drive is walkable up to the chain");
            t.Assert(map.GetNodeOrNull<RoadBarrier>("MansionChain") != null, "the chain is drawn");
            t.Assert(!map.IsStandable(new Vector2I(28, 20)), "the shack blocks");
            t.Assert(map.IsStandable(new Vector2I(33, 28)) && map.IsStandable(new Vector2I(34, 29)),
                "the drive-in's south mouth is open through the treeline");
        });

        await CheckFrame(t, MapIds.DriveIn, map =>
        {
            t.Assert(map.IsStandable(new Vector2I(14, 0)) && map.IsStandable(new Vector2I(15, 1)),
                "the drive-in's north mouth is open through its treeline");
            t.Assert(map.IsStandable(new Vector2I(10, 12)), "the field is walkable");
            t.Assert(!map.IsStandable(new Vector2I(20, 5)), "the boarded concession blocks");
            t.Assert(!map.IsStandable(new Vector2I(10, 22)), "the screen's legs block");
            t.Assert(!map.IsStandable(new Vector2I(6, 10)), "a speaker post blocks");
            t.Assert(!map.IsStandable(new Vector2I(9, 18)), "a bench blocks");
        });

        await CheckFrame(t, MapIds.WestEntry, map =>
        {
            t.Assert(map.IsStandable(new Vector2I(0, 14)) && map.IsStandable(new Vector2I(0, 15)),
                "the west mouth — the road out — is open");
            t.Assert(map.IsStandable(new Vector2I(47, 15)), "and the east mouth toward Billie's");
            t.Assert(!map.IsStandable(new Vector2I(10, 5)), "the motel's room strip blocks");
            t.Assert(map.IsStandable(new Vector2I(10, 8)), "its walkway is open");
            t.Assert(map.IsStandable(new Vector2I(10, 10)), "and so is the parking lot");
            t.Assert(!map.IsStandable(new Vector2I(13, 13)) && !map.IsStandable(new Vector2I(22, 13)),
                "both street light poles block in the verge");
            t.Assert(!map.IsStandable(new Vector2I(26, 19)), "the gas station blocks");
            t.Assert(!map.IsStandable(new Vector2I(34, 11)), "the fireworks stand blocks");
        });

        await CheckFrame(t, MapIds.EastEntry, map =>
        {
            t.Assert(map.IsStandable(new Vector2I(47, 14)) && map.IsStandable(new Vector2I(47, 15)),
                "the east mouth — the road out — is open");
            t.Assert(map.IsStandable(new Vector2I(0, 15)), "and the west mouth toward the east fork");
            t.Assert(!map.IsStandable(new Vector2I(12, 10)), "the police station blocks");
            t.Assert(!map.IsStandable(new Vector2I(25, 10)), "the hardware store blocks");
            t.Assert(!map.IsStandable(new Vector2I(34, 19)), "the salon blocks");
        });
    }

    [SimTest]
    public static async Task Roads_WrapWalksAround(TestContext t)
    {
        // The full loop, walked rather than bussed: step into the west entry's west
        // mouth and come out of the east entry's east one, then leave east and come
        // back in from the west. The one place the wrap actually exists is the exit
        // nodes' wiring, so this is the test that proves the town has no way out.
        Node? main = null;
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

            var maybePlayer = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(maybePlayer != null, "World/Player exists after boot");
            PlayerController player = maybePlayer!;

            t.Assert(WorldSim.Instance.RequestTravel(MapIds.WestEntry, RoadWrap.ArrivalSpawn),
                "travel to the west entry accepted");
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.WestEntry
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "standing at the west entry");

            // Walk out the west edge: the road out of town.
            player.GlobalPosition = new Vector2(8, 15 * 16 + 8); // west exit tile centre
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.EastEntry
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "leaving west wrapped around to the east entry");
            MapRoot? east = FindCurrentMap(main);
            t.Assert(east != null, "east entry instanced under MapHost");
            t.AssertEqual(east!.GetSpawn(RoadWrap.ArrivalSpawn), player.GlobalPosition,
                "arrived at the east entry's wrap marker — rolling in from the east");

            // And out the east edge: the other road out. Same answer.
            player.GlobalPosition = new Vector2(47 * 16 + 8, 15 * 16 + 8);
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.WestEntry
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "leaving east wrapped around to the west entry");
            MapRoot? west = FindCurrentMap(main);
            t.Assert(west != null, "west entry instanced under MapHost");
            t.AssertEqual(west!.GetSpawn(RoadWrap.ArrivalSpawn), player.GlobalPosition,
                "arrived at the west entry's wrap marker — rolling in from the west");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Roads_FarmRoadWalksFromTheFork(TestContext t)
    {
        // The fork's north exit is the one edge with novel geometry — a 2x1 strip on
        // border row 0 — so it gets the same walked proof as the wrap: standable is
        // not "fires BodyEntered".
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);

            var maybePlayer = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(maybePlayer != null, "World/Player exists after boot");
            PlayerController player = maybePlayer!;

            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Fork, "from_farm"),
                "travel to the fork accepted");
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.Fork
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "standing at the fork's north mouth");

            player.GlobalPosition = new Vector2(19 * 16 + 8, 8); // inside the north mouth
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.Farm
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "walking north reached the farm");
            MapRoot? farm = FindCurrentMap(main);
            t.Assert(farm != null, "farm instanced under MapHost");
            t.AssertEqual(farm!.GetSpawn("road"), player.GlobalPosition,
                "arrived on the farm's road marker");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    private static async Task CheckFrame(TestContext t, string mapId, Action<MapRoot> check)
    {
        MapRoot map = MapRegistry.Create(mapId);
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            check(map);
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
        }
    }

    // Same DFS the travel tests use — the graph must not depend on where in a map's
    // tree an exit or door is parented.
    private static void Collect(Node root, string fromMap,
        List<(string, string, string, string)> references)
    {
        switch (root)
        {
            case MapExit exit:
                references.Add((fromMap, exit.Name, exit.TargetMapId, exit.TargetSpawnId));
                break;
            case Door door:
                references.Add((fromMap, door.Name, door.TargetMapId, door.TargetSpawnId));
                break;
        }
        foreach (Node child in root.GetChildren())
            Collect(child, fromMap, references);
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
