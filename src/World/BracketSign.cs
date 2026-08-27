using Godot;
using TheHaunt.Systems;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The hanging-bracket mount (motel handoff §3): a plaque perpendicular to the facade
/// on an iron arm, one bulb above it — the only mount readable side-on. Bars, and
/// anything on a walkable street. Added as a CHILD of its facade sprite at the face's
/// edge; the node origin is the arm's wall attachment, the plaque hangs to +x.
/// </summary>
public partial class BracketSign : Sprite2D
{
    public string Text { get; init; } = "";

    private static readonly Color Iron = new("171310");
    private static readonly Color Plaque = new("2b241d");
    private static readonly Color Letters = new("ede3cb");
    private static readonly Color BulbDay = new("b8b5a5");
    private static readonly Color BulbNight = new("f2b95c");

    private Texture2D? _day, _night;
    private GlowLight? _glow;

    public override void _Ready()
    {
        Centered = false;
        _day = Build(BulbDay);
        _night = Build(BulbNight);
        _glow = new GlowLight
        {
            Size = GlowLight.Falloff.Small,
            Strength = 0.45f,
            // Over the bulb, which sits mid-arm above the plaque.
            Position = new Vector2(PixelFont.Measure(Text) / 2f + 6f, 2f),
        };
        AddChild(_glow);
        Clock.Instance.TenMinuteTicked += OnTimeChanged;
        Clock.Instance.DayStarted += OnTimeChanged;
        Apply();
    }

    public override void _ExitTree()
    {
        Clock.Instance.TenMinuteTicked -= OnTimeChanged;
        Clock.Instance.DayStarted -= OnTimeChanged;
    }

    private void OnTimeChanged(GameTime time) => Apply();

    private void Apply()
    {
        bool night = DayNight.SignsLit(Clock.Instance.Now.MinuteOfDay);
        Texture = night ? _night : _day;
        if (_glow != null)
            _glow.Visible = night;
    }

    private ImageTexture Build(Color bulb)
    {
        int plaqueW = PixelFont.Measure(Text) + 8;
        int w = plaqueW + 6;
        const int h = 24;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        img.FillRect(new Rect2I(0, 6, w - 2, 2), Iron);          // the arm
        img.FillRect(new Rect2I(0, 2, 2, 8), Iron);              // wall plate
        int px = 4;
        img.FillRect(new Rect2I(px + 2, 8, 2, 3), Iron);         // hangers
        img.FillRect(new Rect2I(px + plaqueW - 4, 8, 2, 3), Iron);
        img.FillRect(new Rect2I(px, 11, plaqueW, 11), Plaque);   // the plaque
        img.FillRect(new Rect2I(px + 1, 12, plaqueW - 2, 9), Iron);
        PixelFont.Draw(img, px + 4, 14, Text, Letters);
        img.FillRect(new Rect2I(px + plaqueW / 2 - 1, 2, 3, 3), bulb); // the one bulb
        return ImageTexture.CreateFromImage(img);
    }
}
