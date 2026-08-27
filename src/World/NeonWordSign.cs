using Godot;
using TheHaunt.Systems;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The window mount's neon word (motel handoff §3): a small neon sign inside the
/// glass whose lit state IS the open/closed tell, so the sign never has to be read
/// twice. Lit whenever the predicate says the counter is staffed; unlit it stays
/// faintly visible as dead glass tube — never simply absent. Added as a CHILD of its
/// facade sprite, positioned over a window.
/// </summary>
public partial class NeonWordSign : Sprite2D
{
    public string Word { get; init; } = "OPEN";

    /// <summary>Whether the tube is charged at this minute-of-day.</summary>
    public Func<int, bool> OnAt { get; init; } = _ => false;

    private static readonly Color Lit = new("e05a3f");
    private static readonly Color UnlitDay = new("6d4038");
    private static readonly Color UnlitNight = new("63403a");

    private Texture2D? _lit, _unlitDay, _unlitNight;
    private GlowLight? _glow;

    public override void _Ready()
    {
        _lit = BuildWord(Lit);
        _unlitDay = BuildWord(UnlitDay);
        _unlitNight = BuildWord(UnlitNight);
        _glow = new GlowLight
        {
            Size = GlowLight.Falloff.Small,
            Strength = 0.5f,
            Color = Lit,
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
        int minute = Clock.Instance.Now.MinuteOfDay;
        bool on = OnAt(minute);
        bool night = DayNight.SignsLit(minute);
        Texture = on ? _lit : night ? _unlitNight : _unlitDay;
        if (_glow != null)
            _glow.Visible = on && night;
    }

    private ImageTexture BuildWord(Color color)
    {
        int w = PixelFont.Measure(Word) + 2;
        int h = PixelFont.GlyphHeight + 2;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        PixelFont.Draw(img, 1, 1, Word, color);
        return ImageTexture.CreateFromImage(img);
    }
}
