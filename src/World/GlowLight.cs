using Godot;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// A point light that punches through the <see cref="DayNightTint"/> — lit windows,
/// an open shop door, a lantern. Additive, and the falloff is the hand-dithered
/// sprite from assets/sprites/lights.png rather than a shader gradient, so it stays
/// pixel-honest (art handoff §6).
///
/// Nothing in this town is lit by anything but fire; there is no electric light in
/// the palette.
/// </summary>
public partial class GlowLight : PointLight2D
{
    public const string LightsPath = "res://assets/sprites/lights.png";

    /// <summary>Radial falloff sprites — the spec allows exactly these two sizes.</summary>
    public enum Falloff
    {
        /// <summary>2-tile radius, source rect (0,16,32,32).</summary>
        Small,

        /// <summary>4-tile radius, source rect (32,0,64,64).</summary>
        Large,
    }

    private static readonly Rect2I SmallRegion = new(0, 16, 32, 32);
    private static readonly Rect2I LargeRegion = new(32, 0, 64, 64);

    // PointLight2D refuses an AtlasTexture, so each falloff is cut out of the sheet
    // once into its own texture and shared by every light of that size.
    private static readonly Dictionary<Falloff, Texture2D> Falloffs = new();

    /// <summary>Peak energy at full dark; scaled down by the time of day.</summary>
    public float Strength { get; init; } = 1f;

    public Falloff Size { get; init; } = Falloff.Small;

    /// <summary>Flame flicker: ±4% radius on the 4-frame loop (art handoff §6).</summary>
    public bool Flickers { get; init; }

    private float _flicker = 1f;

    public override void _Ready()
    {
        Texture = FalloffTexture(Size);
        BlendMode = PointLight2D.BlendModeEnum.Add;
        Apply();
    }

    private static Texture2D FalloffTexture(Falloff size)
    {
        if (Falloffs.TryGetValue(size, out Texture2D? cached))
            return cached;

        var sheet = GD.Load<Texture2D>(LightsPath)
            ?? throw new InvalidOperationException($"Light sprites missing at '{LightsPath}'.");
        Image region = sheet.GetImage().GetRegion(size == Falloff.Large ? LargeRegion : SmallRegion);
        Texture2D texture = ImageTexture.CreateFromImage(region);
        Falloffs[size] = texture;
        return texture;
    }

    public override void _Process(double delta) => Apply();

    /// <summary>Driven by the owning <see cref="LampPost"/> so light and flame agree.</summary>
    public void SetFlicker(float scale) => _flicker = scale;

    private void Apply()
    {
        float level = DayNight.LightLevel(Clock.Instance.Now);
        Energy = Strength * level;
        Enabled = level > 0.001f;
        TextureScale = Flickers ? _flicker : 1f;
    }
}
