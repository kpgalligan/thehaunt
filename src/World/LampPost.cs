using Godot;

namespace TheHaunt.World;

/// <summary>
/// Plaza lamp post: a 1x3 prop, the 4-frame flame loop in its lantern, and the
/// <see cref="GlowLight"/> it casts. The flame runs at 6fps and carries the light's
/// ±4% radius variation with it (art handoff §6).
///
/// Position is the bottom-centre of its base tile, like every other <see cref="Prop"/>.
/// </summary>
public partial class LampPost : Node2D
{
    private const float SecondsPerFlameFrame = 1f / 6f;

    private static readonly Rect2 PostSource = new(176, 16, 16, 48);

    // The lantern glass sits 1-8px below the top of the 48px post; the flame frames are
    // drawn feet-down in their 16px cell, so the cell's bottom lands on the glass.
    private const float GlassBottomFromBase = -39f;
    private const float LanternFromBase = -43f;

    // ±4% radius, phased with the frame so the flicker reads as one event.
    private static readonly float[] FlickerByFrame = { 1.00f, 1.04f, 0.98f, 1.02f };

    private Sprite2D? _flame;
    private GlowLight? _light;
    private float _elapsed;
    private int _frame;

    public override void _Ready()
    {
        AddChild(new Prop
        {
            Name = "Post",
            TexturePath = TownProps.TexturePath,
            Source = PostSource,
        });

        var sheet = GD.Load<Texture2D>(GlowLight.LightsPath)
            ?? throw new InvalidOperationException($"Light sprites missing at '{GlowLight.LightsPath}'.");
        _flame = new Sprite2D
        {
            Name = "Flame",
            Texture = sheet,
            RegionEnabled = true,
            Position = new Vector2(0, GlassBottomFromBase),
            Offset = new Vector2(0, -8),
        };
        AddChild(_flame);

        _light = new GlowLight
        {
            Name = "Glow",
            Position = new Vector2(0, LanternFromBase),
            Size = GlowLight.Falloff.Large,
            Strength = 0.75f,
            Flickers = true,
        };
        AddChild(_light);

        ApplyFrame();
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        while (_elapsed >= SecondsPerFlameFrame)
        {
            _elapsed -= SecondsPerFlameFrame;
            _frame = (_frame + 1) % FlickerByFrame.Length;
            ApplyFrame();
        }
    }

    private void ApplyFrame()
    {
        if (_flame != null)
            _flame.RegionRect = new Rect2(96 + _frame * 16, 0, 16, 16);
        _light?.SetFlicker(FlickerByFrame[_frame]);
    }
}
