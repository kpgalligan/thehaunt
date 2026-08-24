using Godot;
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

    public override void _Ready()
    {
        _mapHost = GetNode<Node2D>("World/MapHost");
        _player = GetNode<Player.PlayerController>("World/Player");
        _fade = GetNode<ScreenFade>("UI/ScreenFade");
        GetNode<InteractionPrompt>("UI/InteractionPrompt").Bind(_player.Probe);

        GameState.Instance.StateChanged += OnStateChanged;

        // Load handles quarantining corrupt/too-new files itself; anything but Ok
        // means we start fresh (the unreadable file is already renamed aside, so the
        // upcoming autosaves cannot destroy it).
        if (SaveService.Instance.Load() != LoadResult.Ok)
            SaveService.Instance.NewGame();

        LoadMap(SaveService.Instance.Current.Player.MapId);
        HandleCmdlineArgs();
    }

    public override void _ExitTree()
    {
        GameState.Instance.StateChanged -= OnStateChanged;
    }

    private void LoadMap(string mapId)
    {
        _currentMap?.QueueFree();

        // Foundation: the programmatic test map is the only map. Later this becomes a
        // MapId -> PackedScene registry lookup (see docs/foundation-spec.md §6).
        var map = new TestMap { MapId = mapId };
        _currentMap = map;
        _mapHost.AddChild(map);
        map.ApplyState(SaveService.Instance.Current.GetMap(mapId));

        if (!SaveService.Instance.Current.Player.HasPosition)
            _player.GlobalPosition = map.GetSpawn();
        _player.ApplyCameraLimits(map.GetCameraLimits());
    }

    private void OnStateChanged(GameState.Phase from, GameState.Phase to)
    {
        if (to == GameState.Phase.Sleeping && !_sleepFlowRunning)
            _ = RunSleepFlow();
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
                _ = CaptureScreenshot(args[i + 1]);
        }
    }

    private async Task CaptureScreenshot(string path)
    {
        try
        {
            for (var i = 0; i < 30; i++)
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
