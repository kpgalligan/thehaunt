using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// The motor court's contract (docs/designs/design_handoff_motel_signage): rooms lock
/// individually behind story flags and answer with a line rather than silence, the
/// occupancy tell is story state, the sign circuits follow dusk-to-dawn, and the one
/// pixel typeface can actually draw every sign string the game ships.
/// </summary>
public static class MotelTests
{
    [SimTest]
    public static void Motel_RulesMapRoomsToFlags(TestContext t)
    {
        t.AssertEqual(StoryKeys.MotelRoom1Open, MotelRules.RoomFlag(1), "room 1 flag");
        t.AssertEqual(StoryKeys.MotelRoom2Open, MotelRules.RoomFlag(2), "room 2 flag");
        t.AssertEqual(StoryKeys.MotelRoom3Open, MotelRules.RoomFlag(3), "room 3 flag");
        t.AssertEqual(StoryKeys.MotelRoom4Open, MotelRules.RoomFlag(4), "room 4 flag");
        foreach (int bad in new[] { 0, 5 })
        {
            bool threw = false;
            try { MotelRules.RoomFlag(bad); }
            catch (ArgumentOutOfRangeException) { threw = true; }
            t.Assert(threw, $"room {bad} is not a room");
        }

        var data = GameData.NewGame();
        for (int room = 1; room <= MotelRules.Rooms; room++)
            t.Assert(!MotelRules.IsRoomOpen(data, room), $"room {room} locked on a new game");
        data.TrySetFlag(StoryKeys.MotelRoom2Open, 3);
        t.Assert(MotelRules.IsRoomOpen(data, 2), "room 2 opens on its flag");
        t.Assert(!MotelRules.IsRoomOpen(data, 1), "room 1 stays locked");

        // The occupancy tell: Pell's room, and never decoration.
        t.AssertEqual(3, MotelRules.LitRoom(data), "the lit window is room three");

        t.Assert(!MotelRules.NoVacancy(data), "circuit A dead on a new game");
        data.TrySetFlag(StoryKeys.MotelFull, 5);
        t.Assert(MotelRules.NoVacancy(data), "circuit A lights when the motel is full");
    }

    [SimTest]
    public static void Motel_SignsLitDuskToDawn(TestContext t)
    {
        // Hard cut at the dusk key (18:00 = 720), holding through the dawn tail
        // (till 8:30 = 150) while LightLevel drains — and never in trading hours.
        t.Assert(DayNight.SignsLit(0), "lit at 6:00 AM");
        t.Assert(DayNight.SignsLit(149), "lit just before 8:29 AM");
        t.Assert(!DayNight.SignsLit(150), "dark from 8:30 AM");
        t.Assert(!DayNight.SignsLit(719), "dark just before dusk");
        t.Assert(DayNight.SignsLit(720), "lit at 6:00 PM");
        t.Assert(DayNight.SignsLit(1199), "lit at the clock's clamp");
    }

    [SimTest]
    public static void Motel_PixelFontDrawsEverySignString(TestContext t)
    {
        t.AssertEqual(38, PixelFont.Measure("MOTEL", 2), "MOTEL panel width at 2x");
        t.AssertEqual(27, PixelFont.Measure("VACANCY"), "vacancy line width at 1x");
        t.AssertEqual(0, PixelFont.Measure(""), "empty string");

        // Every glyph the alphabet claims, plus every string a shipped sign uses.
        var img = Image.CreateEmpty(400, 12, false, Image.Format.Rgba8);
        PixelFont.Draw(img, 0, 0, "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-.' ", new Color(1, 1, 1));
        foreach (string sign in new[]
                 { "MOTEL", "NO", "VACANCY", "OFFICE", "ICE", "GAS", "OPEN", "POLICE",
                   "HARDWARE", "SALON", "BAR", "FIREWORKS", "DRIVE-IN", "CLO ED", "SNACKS" })
            PixelFont.Draw(img, 0, 6, sign, new Color(1, 1, 1));

        bool threw = false;
        try { PixelFont.Draw(img, 0, 0, "?", new Color(1, 1, 1)); }
        catch (ArgumentException) { threw = true; }
        t.Assert(threw, "a typo'd sign fails at build, not as a hole");
    }

    [SimTest]
    public static void Motel_SignCircuitsResolve(TestContext t)
    {
        // The one animated element in the game, pinned: nothing runs by day; at
        // night ACANCY holds steady, the V cuts hard out of the first 0.55s of
        // every 4.0s cycle, and NO joins only when the motel is full.
        t.AssertEqual(4.0f, MotelSign.BlinkCycle, "the cycle is 4.0s, never randomised");
        t.AssertEqual(0.55f, MotelSign.BlinkOff, "the V is out for 0.55s of it");

        t.AssertEqual(MotelSign.State.Day, MotelSign.Resolve(false, false, 2f),
            "no circuit runs in daylight");
        t.AssertEqual(MotelSign.State.Day, MotelSign.Resolve(false, true, 2f),
            "not even a full house lights a sign by day");
        t.AssertEqual(MotelSign.State.NightVOff, MotelSign.Resolve(true, false, 0f),
            "the V is out at the top of the cycle");
        t.AssertEqual(MotelSign.State.NightVOff, MotelSign.Resolve(true, false, 0.54f),
            "and stays out through the off window");
        t.AssertEqual(MotelSign.State.NightVOn, MotelSign.Resolve(true, false, 0.55f),
            "hard cut on at exactly 0.55s");
        t.AssertEqual(MotelSign.State.NightVOn,
            MotelSign.Resolve(true, false, MotelSign.BlinkCycle - 0.01f),
            "and on until the cycle wraps");
        t.AssertEqual(MotelSign.State.NightFullVOn, MotelSign.Resolve(true, true, 1f),
            "circuit A joins when the motel is full");
        t.AssertEqual(MotelSign.State.NightFullVOff, MotelSign.Resolve(true, true, 0.1f),
            "and the failing transformer ignores circuit A");
    }

    [SimTest]
    public static async Task Motel_RoomDoorsLockOnFlags(TestContext t)
    {
        SaveService.Instance.NewGame();
        MapRoot map = MapRegistry.Create(MapIds.WestEntry);
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            var office = map.GetNode<Door>("MotelDoor");
            t.Assert(!office.IsLocked, "the office is open at first contact");

            for (int room = 1; room <= MotelRules.Rooms; room++)
            {
                var door = map.GetNode<Door>($"Room{room}Door");
                t.Assert(door.IsLocked, $"room {room} locked on a new game");
                t.Assert(door.LockedMessage.Length > 0, $"room {room} answers, never silence");
                t.Assert(MapRegistry.Contains(door.TargetMapId),
                    $"room {room} leads to a real map the day its flag lands");
                t.AssertEqual(MotelRules.RoomFlag(room), door.RequiredFlag,
                    $"room {room} gated by its own flag");
            }

            // An unlock is a flag stamp, never a repaint: the same node answers live.
            WorldSim.Instance.SetStoryFlag(StoryKeys.MotelRoom2Open);
            t.Assert(!map.GetNode<Door>("Room2Door").IsLocked, "room 2 opens on its flag");
            t.Assert(map.GetNode<Door>("Room1Door").IsLocked, "room 1 unmoved");
            t.Assert(map.GetNode<Door>("Room3Door").IsLocked, "room 3 unmoved");

            // The enforcement point itself: a locked Interact answers with its line
            // and never reaches the travel bus; the unlocked one rides it.
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            var requests = new List<(string MapId, string SpawnId)>();
            void OnTravel(string mapId, string spawnId) => requests.Add((mapId, spawnId));
            WorldSim.Instance.TravelRequested += OnTravel;
            try
            {
                map.GetNode<Door>("Room1Door").Interact(map);
                t.AssertEqual(0, requests.Count, "a locked handle never requests travel");
                map.GetNode<Door>("Room2Door").Interact(map);
                t.AssertEqual(1, requests.Count, "the unlocked door does");
                t.AssertEqual(MapIds.MotelRoom2, requests[0].MapId, "to its own room");
            }
            finally
            {
                WorldSim.Instance.TravelRequested -= OnTravel;
            }
        }
        finally
        {
            map.Free();
            // The unlock stamp above went to the live save — reset so no later test
            // inherits an open room 2.
            SaveService.Instance.NewGame();
        }
    }
}
