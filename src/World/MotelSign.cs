using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The motel's googie pole sign (motel handoff §1-2): atomic starburst, MOTEL panel,
/// the BLANK nameplate (the motel's name is deliberately unwritten — do not invent),
/// and the NO VACANCY neon panel. Neon is bent glass charged per letter, and the
/// motel bought one panel: the tubes read NO VACANCY permanently, and NO is simply
/// not switched on — which is nearly every night, and the sign's whole
/// characterisation.
///
/// Three circuits: A = NO, lit only when the motel is full (never, in Act I);
/// B = ACANCY, steady dusk to dawn; C = V, on B's feed through a failing transformer,
/// so it blinks — 4.0s cycle, 0.55s off, hard cut, never randomised: the regularity
/// is what makes it read as broken rather than atmospheric. This blink is the ONE
/// animated sign in the game; a second flickering sign would make this one stop
/// meaning anything.
///
/// Position is the bottom-centre of the concrete foot, like every Prop.
/// </summary>
public partial class MotelSign : Sprite2D
{
    // Internal so the suite can pin them: the regularity IS the characterisation,
    // and the handoff forbids randomising it.
    internal const float BlinkCycle = 4.0f;
    internal const float BlinkOff = 0.55f;

    // Cabinet geometry (local px), straight from the handoff: 74-wide cabinet,
    // 22px pylon, 4px ink foot.
    private const int W = 74, H = 88;
    private const int PanelY = 42;                    // vacancy panel top

    private static readonly Color Ink900 = new("171310");
    private static readonly Color Ink700 = new("2b241d");
    private static readonly Color Ink500 = new("453a2e");
    private static readonly Color Cream = new("ede3cb");
    private static readonly Color BarnRed = new("a4432f");
    private static readonly Color NeonAqua = new("5fb9b0");
    private static readonly Color NeonRed = new("e05a3f");
    private static readonly Color TubeNight = new("63403a");
    private static readonly Color TubeDay = new("6d4038");
    private static readonly Color BulbDay = new("b8b5a5");
    private static readonly Color BulbNight = new("f2b95c");
    private static readonly Color Pylon = new("575a58");
    private static readonly Color Foot = new("171310");

    internal enum State { Day, NightVOn, NightVOff, NightFullVOn, NightFullVOff }

    private readonly Dictionary<State, Texture2D> _textures = new();
    private GlowLight? _glow;
    private float _elapsed;
    private State _applied = (State)(-1);

    public override void _Ready()
    {
        Offset = new Vector2(0, -H / 2f);
        _glow = new GlowLight
        {
            Name = "PanelGlow",
            Size = GlowLight.Falloff.Large,
            Strength = 0.9f,
            Color = NeonRed,
            Position = new Vector2(0, PanelY + 10 - H),
        };
        AddChild(_glow);
        Apply(ResolveNow(0f));
    }

    public override void _Process(double delta)
    {
        _elapsed = (_elapsed + (float)delta) % BlinkCycle;
        Apply(ResolveNow(_elapsed));
    }

    /// <summary>
    /// The whole circuit truth table, pure so the suite can pin it (MotelTests):
    /// nothing at day; at night ACANCY steady (B), V on B's feed through the failing
    /// transformer (C, off for the first BlinkOff seconds of every cycle), NO only
    /// when the motel is full (A).
    /// </summary>
    internal static State Resolve(bool signsLit, bool noVacancy, float cycleTime)
    {
        if (!signsLit)
            return State.Day;
        bool vOn = cycleTime >= BlinkOff;
        return noVacancy
            ? vOn ? State.NightFullVOn : State.NightFullVOff
            : vOn ? State.NightVOn : State.NightVOff;
    }

    private State ResolveNow(float cycleTime) => Resolve(
        DayNight.SignsLit(Clock.Instance.Now.MinuteOfDay),
        MotelRules.NoVacancy(SaveService.Instance.Current),
        cycleTime);

    private void Apply(State state)
    {
        if (state == _applied)
            return;
        _applied = state;

        if (!_textures.TryGetValue(state, out Texture2D? texture))
        {
            texture = Build(state);
            _textures[state] = texture;
        }
        Texture = texture;

        if (_glow != null)
        {
            _glow.Visible = state != State.Day;
            // The V's share of the glow cuts with the V.
            _glow.Strength = state is State.NightVOff or State.NightFullVOff ? 0.7f : 0.9f;
        }
    }

    private static Texture2D Build(State state)
    {
        bool night = state != State.Day;
        bool vOn = state is State.NightVOn or State.NightFullVOn;
        bool noOn = state is State.NightFullVOn or State.NightFullVOff;
        Color unlitTube = night ? TubeNight : TubeDay;

        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Atomic starburst, top-right.
        img.FillRect(new Rect2I(62, 0, 2, 10), NeonAqua);
        img.FillRect(new Rect2I(58, 4, 10, 2), NeonAqua);

        // Cabinet.
        img.FillRect(new Rect2I(0, 4, W, 62), Ink700);
        img.FillRect(new Rect2I(2, 6, 70, 58), Cream);

        // MOTEL panel, letters at 2x.
        img.FillRect(new Rect2I(4, 8, 66, 16), BarnRed);
        PixelFont.DrawCentered(img, W / 2, 11, "MOTEL", Cream, 2);

        // The nameplate: blank on purpose, one ruled line where the name goes.
        img.FillRect(new Rect2I(4, 26, 66, 14), Ink900);
        img.FillRect(new Rect2I(8, 32, 58, 2), Ink500);

        // Vacancy panel: NO on circuit A, V on circuit C, ACANCY on circuit B.
        img.FillRect(new Rect2I(4, PanelY, 66, 20), Ink900);
        PixelFont.DrawCentered(img, W / 2, PanelY + 3, "NO", noOn ? NeonRed : unlitTube);
        int vacancyX = W / 2 - PixelFont.Measure("VACANCY") / 2;
        PixelFont.Draw(img, vacancyX, PanelY + 11, "V", night && vOn ? NeonRed : unlitTube);
        PixelFont.Draw(img, vacancyX + 4, PanelY + 11, "ACANCY", night ? NeonRed : unlitTube);

        // Bulb rail under the panel.
        for (int x = 4; x <= 68; x += 6)
            img.FillRect(new Rect2I(x, 60, 2, 2), night ? BulbNight : BulbDay);

        // Pylon and foot.
        img.FillRect(new Rect2I(W / 2 - 5, 66, 10, 22), Ink700);
        img.FillRect(new Rect2I(W / 2 - 4, 66, 8, 22), Pylon);
        img.FillRect(new Rect2I(W / 2 - 11, H - 4, 22, 4), Foot);

        return ImageTexture.CreateFromImage(img);
    }
}
