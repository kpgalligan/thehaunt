using Godot;
using TheHaunt.Systems;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The "wall band" sign mount (motel handoff §3): flush 3x5-font letters on a dark
/// band above the door, lit from below after dusk. The civic mount — police station,
/// clinic, anything the town paid for. A pure view like <see cref="StoreFacade"/>:
/// reads the clock, writes nothing. Added as a CHILD of its facade sprite so it draws
/// over the face and Y-sorts with it.
/// </summary>
public partial class WallBandSign : Sprite2D
{
    public string Text { get; init; } = "";

    /// <summary>False for a building nobody lights any more (the hardware store, the
    /// drive-in's concession stand): the band stays dark day and night.</summary>
    public bool LitAtNight { get; init; } = true;

    public int FontScale { get; init; } = 2;

    private static readonly Color Band = new("171310");
    private static readonly Color DayLetters = new("ede3cb");
    private static readonly Color NightLetters = new("f2b95c");

    private Texture2D? _day, _night;
    private GlowLight? _glow;

    public override void _Ready()
    {
        _day = BuildBand(DayLetters);
        _night = LitAtNight ? BuildBand(NightLetters) : _day;
        if (LitAtNight)
        {
            _glow = new GlowLight { Size = GlowLight.Falloff.Small, Strength = 0.4f };
            AddChild(_glow);
        }
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

    private ImageTexture BuildBand(Color letters)
    {
        int w = PixelFont.Measure(Text, FontScale) + 10;
        int h = PixelFont.GlyphHeight * FontScale + 8;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(Band);
        PixelFont.Draw(img, 5, 4, Text, letters, FontScale);
        return ImageTexture.CreateFromImage(img);
    }
}
