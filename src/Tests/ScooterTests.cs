using System.Text.Json.Nodes;
using Godot;
using TheHaunt.Core;
using TheHaunt.Player;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// The scooter's contract (docs/designs/design_handoff_scooter, amended by Kevin
/// 2026-08-27): exactly one exists, parked or under the player; it rides at twice
/// walking speed; it parks on the rider's tile and stays across saves; every save —
/// including every pre-scooter save — has it; and no matter where it was left, it is
/// back outside the farmhouse after sleeping. Interiors park it at the doorstep.
/// </summary>
public static class ScooterTests
{
    [SimTest]
    public static void Scooter_RulesPinTheHandoff(TestContext t)
    {
        t.AssertEqual(2f, ScooterRules.SpeedMultiplier, "handoff: exactly 2x walking speed");
        t.AssertEqual(MapIds.Farm, ScooterRules.HomeMapId, "home is the farm");

        // Parked cell by dismount facing: profile keeps the side view (the hero read,
        // authored facing right), down shows the front, up the three-quarter.
        t.AssertEqual(1, ScooterRules.ParkedColumn(0), "facing down -> front cell");
        t.AssertEqual(0, ScooterRules.ParkedColumn(1), "facing left -> side cell");
        t.AssertEqual(0, ScooterRules.ParkedColumn(2), "facing right -> side cell");
        t.AssertEqual(2, ScooterRules.ParkedColumn(3), "facing up -> three-quarter cell");
        t.Assert(ScooterRules.ParkedFlipH(1), "side view mirrors for a leftward dismount");
        t.Assert(!ScooterRules.ParkedFlipH(2), "side view authored facing right");
        t.Assert(!ScooterRules.ParkedFlipH(0) && !ScooterRules.ParkedFlipH(3),
            "front and three-quarter never mirror");

        // The riding sheet is authored mirrored from the walk sheet: its profile row
        // faces right, so it flips for LEFT where the walk sheet flips for right.
        t.Assert(CharacterSprites.RiderFlipH(1), "riding profile mirrors for left");
        t.Assert(!CharacterSprites.RiderFlipH(2), "riding profile authored facing right");
        t.Assert(CharacterSprites.FlipH(2) && !CharacterSprites.FlipH(1),
            "walk sheet keeps the opposite convention");
    }

    [SimTest]
    public static void Scooter_SheetsMatchTheGrid(TestContext t)
    {
        Image rider = GD.Load<Texture2D>(CharacterSprites.RiderSheetPath).GetImage();
        t.AssertEqual(96, rider.GetWidth(), "riding sheet width");
        t.AssertEqual(96, rider.GetHeight(), "riding sheet height (6x3 of 16x32)");

        Image parked = GD.Load<Texture2D>(Scooter.SheetPath).GetImage();
        t.AssertEqual(48, parked.GetWidth(), "parked sheet width (3 cells)");
        t.AssertEqual(32, parked.GetHeight(), "parked sheet height");

        t.AssertEqual(6, CharacterSprites.RiderFrames, "all six columns are the ride cycle");

        // The three new greens are the only saturated greens in the palette — the deck
        // base must actually be in both sheets, or a re-export drifted the art.
        var deck = new Color("45bf62");
        t.Assert(ContainsColor(rider, deck), "riding sheet carries the deck green");
        t.Assert(ContainsColor(parked, deck), "parked sheet carries the deck green");
    }

    private static bool ContainsColor(Image image, Color color)
    {
        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                Color pixel = image.GetPixel(x, y);
                if (pixel.A > 0.5f && Mathf.Abs(pixel.R - color.R) < 0.01f
                    && Mathf.Abs(pixel.G - color.G) < 0.01f
                    && Mathf.Abs(pixel.B - color.B) < 0.01f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    [SimTest]
    public static void Scooter_EverySaveHasIt(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            // A new game parks it at home, unmounted.
            service.NewGame();
            AssertAtHome(t, service.Current.Scooter, "new game");

            // A pre-scooter save (no Scooter property at all) acquires it at home —
            // that is Kevin's "give it to them now", with no migration needed.
            JsonNode root = JsonNode.Parse(service.SerializeToString())!;
            t.Assert(root.AsObject().Remove("Scooter"), "fixture actually stripped the record");
            service.DeserializeFrom(root.ToJsonString());
            AssertAtHome(t, service.Current.Scooter, "pre-scooter save");

            // A parked position round-trips: map, tile, and facing survive the save.
            service.Current.Scooter.MapId = MapIds.Billies;
            service.Current.Scooter.TileX = 20;
            service.Current.Scooter.TileY = 15;
            service.Current.Scooter.Facing = 2;
            string json = service.SerializeToString();
            service.NewGame();
            service.DeserializeFrom(json);
            t.AssertEqual(MapIds.Billies, service.Current.Scooter.MapId, "map round-trips");
            t.AssertEqual(20, service.Current.Scooter.TileX, "tile x round-trips");
            t.AssertEqual(15, service.Current.Scooter.TileY, "tile y round-trips");
            t.AssertEqual(2, service.Current.Scooter.Facing, "facing round-trips");

            // Load repair: a hostile facing clamps, an empty map id re-parks at home.
            root = JsonNode.Parse(service.SerializeToString())!;
            root["Scooter"]!["Facing"] = 99;
            service.DeserializeFrom(root.ToJsonString());
            t.AssertEqual(3, service.Current.Scooter.Facing, "bad facing clamps");
            root["Scooter"]!["MapId"] = "";
            service.DeserializeFrom(root.ToJsonString());
            AssertAtHome(t, service.Current.Scooter, "empty map id");

            // An UNKNOWN map id is preserved verbatim (preserve-unknown rule) — the
            // overnight reset brings it home; the load must not.
            root["Scooter"]!["MapId"] = "some_future_map";
            service.DeserializeFrom(root.ToJsonString());
            t.AssertEqual("some_future_map", service.Current.Scooter.MapId,
                "unknown parked map survives the load");

            // Hostile tile coords re-park at home — a view at overflow-wrapped
            // off-map coordinates is a scooter lost for a day.
            root["Scooter"]!["MapId"] = MapIds.Billies;
            root["Scooter"]!["TileX"] = 2000000000;
            service.DeserializeFrom(root.ToJsonString());
            AssertAtHome(t, service.Current.Scooter, "hostile tile x");

            // Never ridden — or parked — indoors survives a hand-edited save: both
            // impossible states re-park at home at load.
            root = JsonNode.Parse(service.SerializeToString())!;
            root["Scooter"]!["Mounted"] = true;
            root["Player"]!["MapId"] = MapIds.FarmHouse;
            service.DeserializeFrom(root.ToJsonString());
            AssertAtHome(t, service.Current.Scooter, "mounted inside an interior");
            root["Scooter"]!["Mounted"] = false;
            root["Scooter"]!["MapId"] = MapIds.Salon;
            service.DeserializeFrom(root.ToJsonString());
            AssertAtHome(t, service.Current.Scooter, "parked inside an interior");

            // But mounted on an UNKNOWN player map is kept — outdoors is the safe
            // default for a map this build cannot name.
            root = JsonNode.Parse(service.SerializeToString())!;
            root["Scooter"]!["Mounted"] = true;
            root["Player"]!["MapId"] = "some_future_map";
            service.DeserializeFrom(root.ToJsonString());
            t.Assert(service.Current.Scooter.Mounted, "mounted on an unknown map survives");
        }
        finally
        {
            service.NewGame();
        }
    }

    private static void AssertAtHome(TestContext t, ScooterData scooter, string label)
    {
        t.AssertEqual(ScooterRules.HomeMapId, scooter.MapId, $"{label}: home map");
        t.AssertEqual(ScooterRules.HomeTileX, scooter.TileX, $"{label}: home tile x");
        t.AssertEqual(ScooterRules.HomeTileY, scooter.TileY, $"{label}: home tile y");
        t.AssertEqual(ScooterRules.HomeFacing, scooter.Facing, $"{label}: home facing");
        t.Assert(!scooter.Mounted, $"{label}: parked, not mounted");
    }

    [SimTest]
    public static void Scooter_InteriorTableMatchesTheMaps(TestContext t)
    {
        // MapIds.IsInterior duplicates each map class's IsInterior so load repair can
        // enforce never-ridden-indoors with no node in hand; this is the drift guard
        // that makes the duplication safe. Nodes are never added to the tree.
        foreach (string id in MapIds.All)
        {
            MapRoot map = MapRegistry.Create(id);
            bool actual = map.IsInterior;
            map.Free();
            t.AssertEqual(MapIds.IsInterior(id), actual, $"interior table entry for '{id}'");
        }
    }

    [SimTest]
    public static void Scooter_OvernightBringsItHome(TestContext t)
    {
        // Left at the far end of the map — or asleep mid-ride in a hand-edited save —
        // it is outside the farmhouse by morning. Never stolen (Kevin's rule).
        var data = GameData.NewGame();
        data.Scooter.MapId = MapIds.DriveIn;
        data.Scooter.TileX = 15;
        data.Scooter.TileY = 12;
        data.Scooter.Facing = 3;
        data.Scooter.Mounted = true;
        OvernightSim.Run(data, dayEnding: 4);
        AssertAtHome(t, data.Scooter, "after sleeping");
    }

    [SimTest]
    public static async Task Scooter_MountAndDismountThroughTheBus(TestContext t)
    {
        SaveService service = SaveService.Instance;
        MapRoot? farm = null;
        try
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            farm = MapRegistry.Create(MapIds.Farm);
            t.Host.AddChild(farm);
            await t.WaitFrames(1);

            // Home must be real geometry: standable, and the view spawns on sync.
            var home = new Vector2I(ScooterRules.HomeTileX, ScooterRules.HomeTileY);
            t.Assert(farm.IsStandable(home), "the home tile is standable on the farm");
            WorldSim.Instance.SyncScooterNow();
            await t.WaitFrames(1);
            Scooter? view = LiveScooterView(farm);
            t.Assert(view != null, "parked view exists on the farm");
            t.AssertEqual(
                new Vector2(home.X * MapRoot.TileSize + 8, home.Y * MapRoot.TileSize + 8),
                view!.GlobalPosition, "view stands on the home tile");

            // Mount: refused without control, refused off-map, then taken.
            GameState.Instance.TransitionTo(GameState.Phase.Cutscene);
            t.Assert(!WorldSim.Instance.MountScooter(), "mount refused without control");
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            service.Current.Player.MapId = MapIds.Town;
            t.Assert(!WorldSim.Instance.MountScooter(), "mount refused from another map");
            service.Current.Player.MapId = MapIds.Farm;
            t.Assert(WorldSim.Instance.MountScooter(), "mount taken on the scooter's map");
            t.Assert(WorldSim.Instance.ScooterMounted, "model says mounted");
            t.Assert(!WorldSim.Instance.MountScooter(), "second mount refused");
            await t.WaitFrames(1);
            t.Assert(LiveScooterView(farm) == null,
                "exactly one scooter: the parked view is gone while mounted");

            // Dismount parks on the rider's tile with the rider's facing.
            t.Assert(WorldSim.Instance.DismountScooter(new Vector2I(12, 11), 2), "dismount taken");
            ScooterData scooter = service.Current.Scooter;
            t.Assert(!scooter.Mounted, "model says parked");
            t.AssertEqual(MapIds.Farm, scooter.MapId, "parked on the rider's map");
            t.AssertEqual(12, scooter.TileX, "parked on the rider's tile x");
            t.AssertEqual(11, scooter.TileY, "parked on the rider's tile y");
            t.AssertEqual(2, scooter.Facing, "parked with the rider's facing");
            t.Assert(!WorldSim.Instance.DismountScooter(new Vector2I(1, 1), 0),
                "second dismount refused — only the rider parks it");
            t.Assert(!WorldSim.Instance.ParkScooterAt(MapIds.Town, new Vector2I(1, 1), 0),
                "ParkScooterAt refused when nobody is riding");
            await t.WaitFrames(1);
            view = LiveScooterView(farm);
            t.Assert(view != null, "parked view is back after the dismount");
            t.AssertEqual(
                new Vector2(12 * MapRoot.TileSize + 8, 11 * MapRoot.TileSize + 8),
                view!.GlobalPosition, "view moved to the new parking spot");
        }
        finally
        {
            farm?.Free();
            await t.WaitFrames(1);
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    private static Scooter? LiveScooterView(Node map)
    {
        foreach (Node child in map.GetChildren())
        {
            if (child is Scooter scooter && !scooter.IsQueuedForDeletion())
            {
                return scooter;
            }
        }
        return null;
    }

    [SimTest]
    public static async Task Scooter_InteriorsParkItAtTheDoor(TestContext t)
    {
        Node? main = null;
        try
        {
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            // Keep the story quiet while we ride around (TravelTests pattern).
            WorldSim.Instance.SetStoryFlag(StoryKeys.CrewArrivalDone);
            WorldSim.Instance.SetStoryFlag(StoryKeys.MeetingDone);
            t.Assert(await t.WaitUntil(() => GameState.Instance.PlayerHasControl, 10),
                "control after boot");

            var maybePlayer = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(maybePlayer != null, "World/Player exists after boot");
            PlayerController player = maybePlayer!;

            t.Assert(WorldSim.Instance.MountScooter(), "mounted on the farm");
            Vector2I doorstep = player.FeetTile();

            // Ride through the farmhouse door: the travel flow must park the scooter
            // OUTSIDE — on the farm, at the tile the rider left from — never indoors.
            t.Assert(WorldSim.Instance.RequestTravel(MapIds.FarmHouse, "entry"),
                "travel into the farmhouse accepted");
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.FarmHouse
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "arrived inside");

            ScooterData scooter = SaveService.Instance.Current.Scooter;
            t.Assert(!scooter.Mounted, "auto-dismounted at the door");
            t.AssertEqual(MapIds.Farm, scooter.MapId, "parked on the exterior map");
            t.AssertEqual(doorstep.X, scooter.TileX, "parked at the doorstep x");
            t.AssertEqual(doorstep.Y, scooter.TileY, "parked at the doorstep y");

            // Walk back out: the parked view is waiting on the farm.
            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Farm, "house_door"),
                "travel back out accepted");
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.Farm
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "back on the farm");
            MapRoot? farmMap = FindCurrentMap(main);
            t.Assert(farmMap != null, "farm map instanced");
            Scooter? view = LiveScooterView(farmMap!);
            t.Assert(view != null, "parked view rebuilt with the map");
            t.AssertEqual(
                new Vector2(doorstep.X * MapRoot.TileSize + 8, doorstep.Y * MapRoot.TileSize + 8),
                view!.GlobalPosition, "view stands at the doorstep");

            // Riding is exactly 2x walking, measured at the controller: hold a move
            // action and read the velocity MoveAndSlide was fed.
            t.Assert(WorldSim.Instance.MountScooter(), "remounted for the speed check");
            player.GlobalPosition = farmMap!.GetSpawn("default");
            await t.WaitFrames(2);
            float ridingSpeed = await HeldMoveSpeed(t, player);
            t.AssertEqual(PlayerController.MoveSpeed * ScooterRules.SpeedMultiplier,
                ridingSpeed, "mounted velocity is 2x walk");

            // EXTERIOR travel keeps the rider mounted — only interiors auto-park.
            t.Assert(WorldSim.Instance.RequestTravel(MapIds.Fork, "from_farm"),
                "travel to the fork accepted while mounted");
            t.Assert(await t.WaitUntil(
                () => SaveService.Instance.Current.Player.MapId == MapIds.Fork
                    && GameState.Instance.Current == GameState.Phase.Playing, 10),
                "arrived at the fork");
            t.Assert(SaveService.Instance.Current.Scooter.Mounted,
                "still mounted after exterior travel");

            // The E press decision, on the controller itself: mounted with nothing
            // focused parks it on the tile under the rider's feet.
            await t.WaitFrames(2);
            t.Assert(player.Probe.Focused == null, "nothing focused at the fork spawn");
            player.HandleInteractPress();
            t.Assert(!SaveService.Instance.Current.Scooter.Mounted,
                "E dismounted with nothing focused");
            Vector2I feet = player.FeetTile();
            t.AssertEqual(MapIds.Fork, SaveService.Instance.Current.Scooter.MapId,
                "parked on the fork");
            t.AssertEqual(feet.X, SaveService.Instance.Current.Scooter.TileX,
                "parked under the rider's feet");

            // And on foot the same held action reads plain walking speed.
            float walkSpeed = await HeldMoveSpeed(t, player);
            t.AssertEqual(PlayerController.MoveSpeed, walkSpeed, "dismounted velocity is 1x walk");
        }
        finally
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
        }
    }

    // Holds move_right across a few REAL physics ticks and returns the speed the
    // controller fed MoveAndSlide. Physics runs at wall-clock rate while headless
    // process frames spin much faster, so this waits on the physics-frame counter,
    // never on process frames.
    private static async Task<float> HeldMoveSpeed(TestContext t, PlayerController player)
    {
        ulong start = Engine.GetPhysicsFrames();
        Input.ActionPress("move_right");
        t.Assert(await t.WaitUntil(() => Engine.GetPhysicsFrames() >= start + 3, 5),
            "physics ticked under the held move action");
        float speed = player.Velocity.Length();
        Input.ActionRelease("move_right");
        return speed;
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
}
