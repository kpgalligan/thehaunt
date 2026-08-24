using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.Player;

public partial class PlayerController : CharacterBody2D, IPersistentSystem
{
    public const float MoveSpeed = 80f; // px/sec

    private const int SpriteWidth = 16;
    private const int SpriteHeight = 22;

    private static readonly Color HairColor = new("5a4a3a");
    private static readonly Color SkinColor = new("e8c8a0");
    private static readonly Color TunicColor = new("4a6ab0");
    private static readonly Color EyeColor = new("2a2a2a");

    public int Facing { get; private set; } // 0=down 1=left 2=right 3=up
    public InteractionProbe Probe { get; private set; } = null!;

    private readonly ImageTexture[] _facingTextures = new ImageTexture[4];
    private Sprite2D? _sprite;
    private Camera2D? _camera;
    private Rect2? _pendingCameraLimits;

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

        for (int i = 0; i < _facingTextures.Length; i++)
            _facingTextures[i] = CreateFacingTexture(i);

        _sprite = new Sprite2D { Position = new Vector2(0, -3) };
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
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = input * MoveSpeed;
        MoveAndSlide();

        if (input != Vector2.Zero)
        {
            // Dominant axis wins; ties go to horizontal.
            int facing = Mathf.Abs(input.X) >= Mathf.Abs(input.Y)
                ? (input.X < 0 ? 1 : 2)
                : (input.Y < 0 ? 3 : 0);
            if (facing != Facing)
                ApplyFacing(facing);
        }

        if (Input.IsActionJustPressed("interact"))
            Probe.TryInteract(this);
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
        if (_sprite != null)
            _sprite.Texture = _facingTextures[Facing];
        if (Probe != null)
            Probe.SetFacing(Facing);
    }

    // 16x22 placeholder: hair on top, face below, tunic for the body, 2 px of
    // transparent margin on each side. Facing up shows the back of the head
    // (hair extends over the face rows); eyes shift with left/right facing.
    private static ImageTexture CreateFacingTexture(int facing)
    {
        var img = Image.CreateEmpty(SpriteWidth, SpriteHeight, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        int hairBottomRow = facing == 3 ? 9 : 5;
        for (int y = 0; y < SpriteHeight; y++)
        {
            Color color = y <= hairBottomRow ? HairColor
                : y <= 11 ? SkinColor
                : TunicColor;
            for (int x = 2; x <= 13; x++)
                img.SetPixel(x, y, color);
        }

        if (facing != 3)
        {
            int offset = facing switch { 1 => -2, 2 => 2, _ => 0 };
            foreach (int eyeX in new[] { 5 + offset, 10 + offset })
            {
                img.SetPixel(eyeX, 8, EyeColor);
                img.SetPixel(eyeX, 9, EyeColor);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
