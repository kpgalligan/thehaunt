using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Player;

public partial class PlayerController : CharacterBody2D, IPersistentSystem
{
    public const float MoveSpeed = 80f; // px/sec

    private const int TileSize = 16;
    private const float UseToolCooldown = 0.25f; // seconds

    private static readonly StringName[] HotbarActions =
    {
        "hotbar_1", "hotbar_2", "hotbar_3", "hotbar_4", "hotbar_5",
        "hotbar_6", "hotbar_7", "hotbar_8", "hotbar_9", "hotbar_10",
    };

    private static readonly Color TunicColor = new("4a6ab0");

    public int Facing { get; private set; } // 0=down 1=left 2=right 3=up
    public InteractionProbe Probe { get; private set; } = null!;

    private CharacterSprite? _sprite;
    private Camera2D? _camera;
    private Rect2? _pendingCameraLimits;
    private float _sinceLastToolUse = UseToolCooldown; // start ready
    private bool _hadControlLastPhysicsFrame;

    public override void _EnterTree()
    {
        SaveService.Instance.Register(this);
    }

    public override void _ExitTree()
    {
        SaveService.Instance.Unregister(this);
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 1;

        _sprite = new CharacterSprite { Tunic = TunicColor };
        AddChild(_sprite);

        AddChild(new CollisionShape2D
        {
            Position = new Vector2(0, 6), // feet
            Shape = new RectangleShape2D { Size = new Vector2(12, 8) },
        });

        Probe = new InteractionProbe();
        AddChild(Probe);

        _camera = new Camera2D { PositionSmoothingEnabled = false };
        AddChild(_camera);
        if (_pendingCameraLimits is { } limits)
            ApplyCameraLimits(limits);

        ApplyFacing(Facing);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!GameState.Instance.PlayerHasControl)
        {
            _hadControlLastPhysicsFrame = false;
            Velocity = Vector2.Zero;
            MoveAndSlide();
            _sprite?.SetMoving(false);
            return;
        }

        // The press that CLOSED a dialogue is handled in _UnhandledInput, which runs
        // before this frame's physics — but the Input singleton still reports it as
        // just-pressed here, and the phase was restored synchronously in that same
        // dispatch. Swallow action presses on the first frame control returns, or the
        // closing E re-opens the conversation (and a closing click swings the tool).
        bool firstFrameBack = !_hadControlLastPhysicsFrame;
        _hadControlLastPhysicsFrame = true;

        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = input * MoveSpeed;
        MoveAndSlide();
        _sprite?.SetMoving(input != Vector2.Zero);

        if (input != Vector2.Zero)
        {
            // Dominant axis wins; ties go to horizontal.
            int facing = Mathf.Abs(input.X) >= Mathf.Abs(input.Y)
                ? (input.X < 0 ? 1 : 2)
                : (input.Y < 0 ? 3 : 0);
            if (facing != Facing)
                ApplyFacing(facing);
        }

        if (!firstFrameBack && Input.IsActionJustPressed("interact"))
            Probe.TryInteract(this);

        _sinceLastToolUse += (float)delta;
        if (!firstFrameBack && Input.IsActionJustPressed("use_tool") && _sinceLastToolUse >= UseToolCooldown)
        {
            _sinceLastToolUse = 0f;
            WorldSim.Instance.UseSelectedItem(TargetTile());
        }

        for (int i = 0; i < HotbarActions.Length; i++)
        {
            if (Input.IsActionJustPressed(HotbarActions[i]))
                WorldSim.Instance.SelectSlot(i);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!GameState.Instance.PlayerHasControl)
            return;

        // Wheel actions land press+release within one frame, so polling
        // IsActionJustPressed in _PhysicsProcess would miss them.
        int step;
        if (@event.IsActionPressed("hotbar_next"))
            step = 1;
        else if (@event.IsActionPressed("hotbar_prev"))
            step = -1;
        else
            return;

        int selected = SaveService.Instance.Current.Player.Inventory.SelectedSlot;
        WorldSim.Instance.SelectSlot(
            (selected + step + InventoryData.Capacity) % InventoryData.Capacity);
        GetViewport().SetInputAsHandled();
    }

    // Feet tile + facing direction, computed directly — never from the probe
    // position (feet + dir*14 rounds back into the player's own tile when
    // feet%16 < 2).
    public Vector2I TargetTile()
    {
        Vector2 feet = GlobalPosition + new Vector2(0, 6);
        var feetTile = new Vector2I(
            Mathf.FloorToInt(feet.X / TileSize),
            Mathf.FloorToInt(feet.Y / TileSize));
        Vector2I dir = Facing switch
        {
            1 => new Vector2I(-1, 0),
            2 => new Vector2I(1, 0),
            3 => new Vector2I(0, -1),
            _ => new Vector2I(0, 1),
        };
        return feetTile + dir;
    }

    public void ApplyCameraLimits(Rect2 limits)
    {
        if (_camera == null)
        {
            // Camera is built in _Ready; remember limits applied before then.
            _pendingCameraLimits = limits;
            return;
        }
        _camera.LimitLeft = (int)limits.Position.X;
        _camera.LimitTop = (int)limits.Position.Y;
        _camera.LimitRight = (int)limits.End.X;
        _camera.LimitBottom = (int)limits.End.Y;
    }

    public void WriteState(GameData data)
    {
        data.Player.X = GlobalPosition.X;
        data.Player.Y = GlobalPosition.Y;
        data.Player.Facing = Facing;
        data.Player.HasPosition = true;
    }

    public void ReadState(GameData data)
    {
        if (!data.Player.HasPosition)
            return;
        GlobalPosition = new Vector2(data.Player.X, data.Player.Y);
        ApplyFacing(data.Player.Facing);
    }

    private void ApplyFacing(int facing)
    {
        // Clamp: Facing can arrive from a hand-edited save file via ReadState.
        Facing = Math.Clamp(facing, 0, 3);
        _sprite?.SetFacing(Facing);
        if (Probe != null)
            Probe.SetFacing(Facing);
    }
}
