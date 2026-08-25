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
    private MapRoot? _currentMap;
    private bool _sleepFlowRunning;
    private bool _travelRunning;

    public override void _Ready()
    {
        _mapHost = GetNode<Node2D>("World/MapHost");
        _player = GetNode<Player.PlayerController>("World/Player");
        _fade = GetNode<ScreenFade>("UI/ScreenFade");
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
