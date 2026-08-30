using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.UI;
using TheHaunt.World;

namespace TheHaunt;

public partial class Main : Node2D
{
    private Node2D _mapHost = null!;
    private Player.PlayerController _player = null!;
    private DayNightTint _lighting = null!;
    private ScreenFade _fade = null!;
    private OvernightReportUi _overnightReport = null!;
    private MapRoot? _currentMap;
    private string _bootSpawnId = "default";
    private bool _sleepFlowRunning;
    private bool _travelRunning;

    public override void _Ready()
    {
        _mapHost = GetNode<Node2D>("World/MapHost");
        _player = GetNode<Player.PlayerController>("World/Player");
        _lighting = GetNode<DayNightTint>("World/Lighting");
        _fade = GetNode<ScreenFade>("UI/ScreenFade");
        _overnightReport = GetNode<OvernightReportUi>("UI/OvernightReport");
        GetNode<InteractionPrompt>("UI/InteractionPrompt").Bind(_player.Probe);

        GameState.Instance.StateChanged += OnStateChanged;
        WorldSim.Instance.TravelRequested += OnTravelRequested;

        // Load handles quarantining corrupt/too-new files itself; anything but Ok
        // means we start fresh (the unreadable file is already renamed aside, so the
        // upcoming autosaves cannot destroy it).
        if (SaveService.Instance.Load() != LoadResult.Ok)
            SaveService.Instance.NewGame();

        // Before LoadMap: --start-map overrides the map the boot lands on.
        HandleCmdlineArgs();
        LoadMap(SaveService.Instance.Current.Player.MapId);
    }

    public override void _ExitTree()
    {
        GameState.Instance.StateChanged -= OnStateChanged;
        WorldSim.Instance.TravelRequested -= OnTravelRequested;
    }

    // Boot-time wrapper around the registry: no fade, no phase change (travel owns those).
    private void LoadMap(string mapId)
    {
        _currentMap?.QueueFree();

        if (!MapRegistry.Contains(mapId))
        {
            // Unknown id from a save: fall back without touching GameData.Maps — the
            // unknown map's state must survive the next autosave (preservation rule).
            GD.PushError($"Unknown map id '{mapId}' in save; falling back to '{MapIds.Farm}'.");
            mapId = MapIds.Farm;
            SaveService.Instance.Current.Player.HasPosition = false;
        }

        var map = MapRegistry.Create(mapId);
        _currentMap = map;
        _mapHost.AddChild(map);
        WorldSim.Instance.EnsureObstacles(map);   // first visit grows the field's trees and rocks
        map.ApplyState(SaveService.Instance.Current.GetMap(map.MapId));
        // Boot order: the Lighting node's own _Ready ran before the save was loaded,
        // so the first correct tint is the one applied here.
        _lighting.SetMap(map);

        // Through the bus like RunTravel: keeps Player.MapId coherent after an
        // unknown-id fallback and stages NPCs at boot (AfterLoad fired before any
        // map was registered, so its sync had nothing to paint).
        WorldSim.Instance.CompleteTravel(map.MapId);

        // A pre-3b save can hold a position that new geometry (building facades,
        // doors) has since swallowed; loading it verbatim embeds the player in
        // collision. Bounce to the spawn — the file itself is never rewritten.
        var playerData = SaveService.Instance.Current.Player;
        if (playerData.HasPosition)
        {
            var feetTile = new Vector2I(
                Mathf.FloorToInt(playerData.X / MapRoot.TileSize),
                Mathf.FloorToInt((playerData.Y + 6) / MapRoot.TileSize));
            if (!map.IsStandable(feetTile))
            {
                GD.PushWarning($"Saved position ({playerData.X:0},{playerData.Y:0}) is no longer standable on '{map.MapId}'; respawning.");
                playerData.HasPosition = false;
            }
        }

        if (!SaveService.Instance.Current.Player.HasPosition)
            _player.GlobalPosition = map.GetSpawn(_bootSpawnId);
        _player.ApplyCameraLimits(map.GetCameraLimits());
    }

    private void OnStateChanged(GameState.Phase from, GameState.Phase to)
    {
        if (to == GameState.Phase.Sleeping && !_sleepFlowRunning)
            _ = RunSleepFlow();
    }

    private void OnTravelRequested(string mapId, string spawnId, Vector2 arrivalOffset)
    {
        if (!_travelRunning)
            _ = RunTravel(mapId, spawnId, arrivalOffset);
    }

    private async Task RunTravel(string mapId, string spawnId, Vector2 arrivalOffset = default)
    {
        _travelRunning = true;
        GameState.Instance.TransitionTo(GameState.Phase.Cutscene);   // clock + player frozen; tree NOT paused
        // Where the rider stood when the door was used — captured before the fade
        // frees the map, for the interior auto-dismount below.
        string fromMapId = SaveService.Instance.Current.Player.MapId;
        Vector2I fromTile = _player.FeetTile();
        int fromFacing = _player.Facing;
        try
        {
            // MapExit.BodyEntered fires during the physics flush — the awaited fade
            // must come BEFORE any tree mutation (load-bearing ordering).
            await _fade.FadeOut(0.25);
            _currentMap?.QueueFree();               // FindRegisteredMap's IsQueuedForDeletion guard covers the same-frame window
            var map = MapRegistry.Create(mapId);
            // Never ridden indoors (scooter handoff): entering an interior parks the
            // scooter outside, at the doorstep the rider just left. No-op unless mounted.
            if (map.IsInterior)
                WorldSim.Instance.ParkScooterAt(fromMapId, fromTile, fromFacing);
            _currentMap = map;
            _mapHost.AddChild(map);
            WorldSim.Instance.EnsureObstacles(map);   // first visit grows the field's trees and rocks
            map.ApplyState(SaveService.Instance.Current.GetMap(map.MapId));
            _lighting.SetMap(map);                                   // interior/exterior key, set while black
            WorldSim.Instance.CompleteTravel(map.MapId);             // model write via the bus + NPC sync
            _player.GlobalPosition = map.GetArrival(spawnId, arrivalOffset);   // node-owned volatile state, set while black
            _player.ApplyCameraLimits(map.GetCameraLimits());
            await _fade.FadeIn(0.25);
        }
        catch (Exception e)
        {
            GD.PushError($"Travel failed: {e}");
        }
        finally
        {
            _travelRunning = false;
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    private async Task RunSleepFlow()
    {
        _sleepFlowRunning = true;
        try
        {
            await _fade.FadeOut();
            Clock.Instance.AdvanceToDayStart();
            // The overslept summons: told to attend the town meeting and went to bed
            // instead. Stamp intro.overslept (stages the mayor, selects the beat
            // variant) and relocate the wake to the hall while the screen is still
            // black — both BEFORE the autosave, so quitting mid-morning reloads into
            // the same relocated morning. The beat itself fires off the finally's
            // return to Playing, through the director's normal deferred check.
            if (IntroRules.WakesAtTownHall(SaveService.Instance.Current))
            {
                WorldSim.Instance.SetStoryFlag(StoryKeys.Overslept);
                WakeAt(MapIds.TownHall, "entry");
            }
            SaveService.Instance.Save();
            await _fade.FadeIn();
            // Money is credited + autosaved above, so quitting mid-card loses only the
            // popup. The phase is still Sleeping while the card is up (player frozen,
            // PauseMenu inert, StoryDirector bails); the finally restores Playing on
            // dismissal, and the dismissing press is swallowed by the controller's
            // _hadControlLastPhysicsFrame guard.
            Task card = _overnightReport.ShowIfPendingAsync();
            bool showed = !card.IsCompleted;
            await card;
            // Mash grace: the player dismissed the card standing at the bed with E — the
            // same key that re-sleeps. Hold Sleeping briefly so a habit double-tap dies
            // while control is still off (the one-frame controller guard only covers the
            // dismissing press itself). Zero-proceeds mornings keep exact pre-3b timing.
            if (showed)
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        }
        catch (Exception e)
        {
            GD.PushError($"Sleep flow failed: {e}");
        }
        finally
        {
            _sleepFlowRunning = false;
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    // The sleep flow's scripted relocation (IntroRules.WakesAtTownHall): the same
    // swap RunTravel's middle performs, minus the fades (the sleep fade already owns
    // the black) and minus the doorstep scooter parking (OvernightSim has already
    // parked it home). Runs before the morning autosave, which then captures the new
    // map and position through PlayerController.WriteState.
    private void WakeAt(string mapId, string spawnId)
    {
        _currentMap?.QueueFree();
        var map = MapRegistry.Create(mapId);
        _currentMap = map;
        _mapHost.AddChild(map);
        WorldSim.Instance.EnsureObstacles(map);   // no-op on interiors (no candidates)
        map.ApplyState(SaveService.Instance.Current.GetMap(map.MapId));
        _lighting.SetMap(map);
        WorldSim.Instance.CompleteTravel(map.MapId);
        _player.GlobalPosition = map.GetSpawn(spawnId);
        _player.ApplyCameraLimits(map.GetCameraLimits());
    }

    private void HandleCmdlineArgs()
    {
        // Only the real boot instance reacts to CLI flags — not Mains instanced by tests.
        if (GetTree().CurrentScene != this)
            return;

        var args = OS.GetCmdlineUserArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--screenshot")
                _ = CaptureScreenshot(args[i + 1], ParseFrames(args));

            // Dev-only: boot into a specific map so --screenshot can capture town and
            // town-hall placeholders. Unknown ids are ignored (normal boot proceeds).
            if (args[i] == "--start-map" && MapRegistry.Contains(args[i + 1]))
            {
                SaveService.Instance.Current.Player.MapId = args[i + 1];
                SaveService.Instance.Current.Player.HasPosition = false;
                // The dev teleport skips the door flow, so it re-applies the load
                // repair itself: never mounted indoors — the scooter goes home.
                if (MapIds.IsInterior(args[i + 1]) && SaveService.Instance.Current.Scooter.Mounted)
                    SaveService.Instance.Current.Scooter = ScooterData.AtHome();
            }

            // Dev-only: land the boot on a named spawn marker instead of the map's
            // default, so --screenshot can frame a corner of a map that has no door
            // leading to it. Ignored once a save carries a position.
            if (args[i] == "--spawn")
            {
                _bootSpawnId = args[i + 1];
                SaveService.Instance.Current.Player.HasPosition = false;
            }

            // Dev-only: advance the in-memory clock (never saved unless the run sleeps)
            // so --screenshot can catch time-gated staging like the shopkeeper's hours.
            // Through the clock, not GameData — the model already synced at load.
            if (args[i] == "--add-minutes" && int.TryParse(args[i + 1], out int extra) && extra > 0)
                Clock.Instance.AdvanceMinutes(extra);

            // Dev-only: pop one of the 3b UIs after boot so --screenshot can capture it.
            if (args[i] == "--open-ui")
                CallDeferred(nameof(OpenUiForScreenshot), args[i + 1]);

            // Dev-only: select the named tool item and hold use_tool from boot, so
            // --screenshot can capture the work animation (e.g. --work-tool hoe
            // --screenshot-frames 16 lands around the impact frame).
            if (args[i] == "--work-tool")
                CallDeferred(nameof(HoldToolForScreenshot), args[i + 1]);

            // Dev-only: stamp the deed and put a car on a lift (the named service,
            // arriving "now"), so --screenshot can capture the shop floor without
            // waiting out the 6% hourly roll. Repeat the flag for a second car.
            if (args[i] == "--garage-job")
                CallDeferred(nameof(AddGarageJobForScreenshot), args[i + 1]);
        }

        // Dev-only (flag, no value): mount the scooter after boot so --screenshot can
        // capture the riding sprite. Refused by the bus unless the boot map holds it.
        foreach (string arg in args)
        {
            if (arg == "--ride")
                CallDeferred(nameof(MountScooterForScreenshot));
        }
    }

    private void MountScooterForScreenshot() => WorldSim.Instance.MountScooter();

    // Deferred so the boot's LoadMap/phase state has fully settled first. In-memory
    // dev seeding only — the real paths are BuyGarage and the hourly roll.
    private void AddGarageJobForScreenshot(string serviceId)
    {
        if (GarageServices.TryGet(serviceId) is null)
        {
            GD.PushError($"--garage-job: unknown service '{serviceId}'.");
            return;
        }
        GameData data = SaveService.Instance.Current;
        WorldSim.Instance.SetStoryFlag(StoryKeys.GarageDeed);   // implies ownership
        if (GarageOpsRules.FreeLift(data) is not int lift)
            return;   // both bays taken — the flag can only seed MaxCars cars
        data.GarageJobs.Add(new GarageJobRecord
        {
            ServiceId = serviceId,
            ArrivalDay = Clock.Instance.Now.DayIndex,
            ArrivalHour = Clock.Instance.Now.AbsoluteHour,
            Lift = lift,
        });
        _currentMap?.ApplyState(data.GetMap(_currentMap.MapId));
    }

    // Deferred so the boot's LoadMap/phase state has fully settled first. The held
    // action never releases; the loop repeats until the process exits.
    private void HoldToolForScreenshot(string itemId)
    {
        InventoryData inv = SaveService.Instance.Current.Player.Inventory;
        for (int i = 0; i < inv.Slots.Count; i++)
        {
            if (inv.SlotAt(i)?.ItemId == itemId)
            {
                WorldSim.Instance.SelectSlot(i);
                Input.ActionPress("use_tool");
                return;
            }
        }
        // A fresh boot has empty hands (the kit waits in the barn chest) — conjure the
        // named tool into slot 0 rather than walking the errand. In-memory only.
        inv.Slots[0] = new ItemStackRecord { ItemId = itemId, Count = 1 };
        WorldSim.Instance.SelectSlot(0);
        Input.ActionPress("use_tool");
    }

    // Deferred so the boot's LoadMap/phase state has fully settled first.
    private void OpenUiForScreenshot(string which)
    {
        switch (which)
        {
            case "chest":
                WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest);
                break;
            case "shop":
                WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore);
                break;
            case "help":
                Input.ParseInputEvent(new InputEventAction { Action = "toggle_help", Pressed = true });
                break;
            case "mail":
                WorldSim.Instance.OpenMailbox();
                break;
            case "garage":
                WorldSim.Instance.OpenGarageSale();
                break;
            case "letter":
                // Mail panel with the first letter opened: the queued Enter activates
                // the focused list row through the GUI stage next frame.
                WorldSim.Instance.OpenMailbox();
                Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
                Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false });
                break;
            case "quests":
                Input.ParseInputEvent(new InputEventAction { Action = "toggle_quests", Pressed = true });
                break;
            case "skills":
                Input.ParseInputEvent(new InputEventAction { Action = "toggle_skills", Pressed = true });
                break;
        }
    }

    // Dev-only: "--screenshot-frames <n>" delays the capture (e.g. past a beat's
    // staging timer to catch the dialogue box). Default 30.
    private static int ParseFrames(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--screenshot-frames" && int.TryParse(args[i + 1], out int n) && n > 0)
                return n;
        return 30;
    }

    private async Task CaptureScreenshot(string path, int frames)
    {
        try
        {
            for (var i = 0; i < frames; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng(path);
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError($"Screenshot capture failed: {e}");
        }
    }
}
