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
    private ScreenFade _fade = null!;
    private OvernightReportUi _overnightReport = null!;
    private MapRoot? _currentMap;
    private bool _sleepFlowRunning;
    private bool _travelRunning;

    public override void _Ready()
    {
        _mapHost = GetNode<Node2D>("World/MapHost");
        _player = GetNode<Player.PlayerController>("World/Player");
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
        map.ApplyState(SaveService.Instance.Current.GetMap(map.MapId));

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
            _player.GlobalPosition = map.GetSpawn();
        _player.ApplyCameraLimits(map.GetCameraLimits());
    }

    private void OnStateChanged(GameState.Phase from, GameState.Phase to)
    {
        if (to == GameState.Phase.Sleeping && !_sleepFlowRunning)
            _ = RunSleepFlow();
    }

    private void OnTravelRequested(string mapId, string spawnId)
    {
        if (!_travelRunning)
            _ = RunTravel(mapId, spawnId);
    }

    private async Task RunTravel(string mapId, string spawnId)
    {
        _travelRunning = true;
        GameState.Instance.TransitionTo(GameState.Phase.Cutscene);   // clock + player frozen; tree NOT paused
        try
        {
            // MapExit.BodyEntered fires during the physics flush — the awaited fade
            // must come BEFORE any tree mutation (load-bearing ordering).
            await _fade.FadeOut(0.25);
            _currentMap?.QueueFree();               // FindRegisteredMap's IsQueuedForDeletion guard covers the same-frame window
            var map = MapRegistry.Create(mapId);
            _currentMap = map;
            _mapHost.AddChild(map);
            map.ApplyState(SaveService.Instance.Current.GetMap(map.MapId));
            WorldSim.Instance.CompleteTravel(map.MapId);             // model write via the bus + NPC sync
            _player.GlobalPosition = map.GetSpawn(spawnId);          // node-owned volatile state, set while black
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
            }

            // Dev-only: advance the in-memory clock (never saved unless the run sleeps)
            // so --screenshot can catch time-gated staging like the shopkeeper's hours.
            // Through the clock, not GameData — the model already synced at load.
            if (args[i] == "--add-minutes" && int.TryParse(args[i + 1], out int extra) && extra > 0)
                Clock.Instance.AdvanceMinutes(extra);

            // Dev-only: pop one of the 3b UIs after boot so --screenshot can capture it.
            if (args[i] == "--open-ui")
                CallDeferred(nameof(OpenUiForScreenshot), args[i + 1]);
        }
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
