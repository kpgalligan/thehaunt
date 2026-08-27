using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The motor court's drawn face (motel handoff §1): the late-1950s googie office —
/// upswept eave, aqua stripe, plate glass — and the four-room strip with its numbered
/// doors and the ice alcove at the east end. Authored from the handoff's pixel spec
/// (the mockup PNGs are composites, not atlases), day and night as two textures
/// swapped on time of day, never a tint — the blue wash stays <see cref="DayNightTint"/>'s.
///
/// At night the lobby is lit and exactly ONE guest room window is lit — the occupancy
/// tell, driven by <see cref="MotelRules.LitRoom"/>, never by decoration. A pure
/// view: reads clock and story state, writes nothing. Position is the bottom-centre
/// of the drawn face, like every facade.
/// </summary>
public partial class MotelFacade : Sprite2D
{
    private const int W = 400, H = 108;   // the last 2 rows are the ink base band below the kick plates

    private static readonly Color Ink700 = new("2b241d");
    private static readonly Color Ink900 = new("171310");
    private static readonly Color Cream = new("ede3cb");
    private static readonly Color StonePale = new("b8b5a5");
    private static readonly Color StoneShade = new("575a58");
    private static readonly Color StoneDark = new("3e4241");
    private static readonly Color BarnRed = new("a4432f");
    private static readonly Color NeonAqua = new("5fb9b0");
    private static readonly Color GlassDay = new("2e5566");
    private static readonly Color GlassNightLit = new("f2b95c");
    private static readonly Color OfficeGlassNight = new("2a2a20");
    private static readonly Color Reflection = new("5c8fa3");
    private static readonly Color Lamp = new("f2b95c");

    private Texture2D? _day;
    private Texture2D? _night;
    private int _nightLitRoom;

    private GlowLight? _lobbyGlow, _roomGlow, _iceGlow, _canopyGlow;

    public override void _Ready()
    {
        Offset = new Vector2(0, -H / 2f);

        // Light pools from the handoff's night table, positioned relative to the
        // face's bottom-centre anchor.
        _lobbyGlow = new GlowLight
        {
            Name = "LobbyGlow",
            Size = GlowLight.Falloff.Large,
            Strength = 0.7f,
            Position = new Vector2(-156, -8),
        };
        _roomGlow = new GlowLight
        {
            Name = "RoomGlow",
            Size = GlowLight.Falloff.Large,
            Strength = 0.55f,
            Position = RoomGlowPosition(3),
        };
        _iceGlow = new GlowLight
        {
            Name = "IceGlow",
            Size = GlowLight.Falloff.Small,
            Strength = 0.4f,
            Color = NeonAqua,
            Position = new Vector2(182, -8),
        };
        _canopyGlow = new GlowLight
        {
            Name = "CanopyGlow",
            Size = GlowLight.Falloff.Small,
            Strength = 0.25f,
            Color = NeonAqua,
            Position = new Vector2(-78, 10),
        };
        AddChild(_lobbyGlow);
        AddChild(_roomGlow);
        AddChild(_iceGlow);
        AddChild(_canopyGlow);

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

    /// <summary>Window centre of guest room <paramref name="room"/> (1-4), relative
    /// to the face's anchor.</summary>
    private static Vector2 RoomGlowPosition(int room) => new(-46 + 68 * (room - 1), -12);

    private void Apply()
    {
        bool night = DayNight.SignsLit(Clock.Instance.Now.MinuteOfDay);
        int litRoom = MotelRules.LitRoom(SaveService.Instance.Current);

        if (night && (_night == null || _nightLitRoom != litRoom))
        {
            _night = Build(night: true, litRoom);
            _nightLitRoom = litRoom;
        }
        _day ??= Build(night: false, 0);
        Texture = night ? _night : _day;

        if (_roomGlow != null)
            _roomGlow.Position = RoomGlowPosition(litRoom);
        foreach (Node child in GetChildren())
        {
            if (child is GlowLight glow)
                glow.Visible = night;
        }
    }

    // ------------------------------------------------------------------
    // Authoring. Handoff pixel coords map to texture coords as (x-2, y-38).
    // ------------------------------------------------------------------

    private static Texture2D Build(bool night, int litRoom)
    {
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        DrawOffice(img, night);
        DrawStrip(img, night, litRoom);
        return ImageTexture.CreateFromImage(img);
    }

    private static void DrawOffice(Image img, bool night)
    {
        img.FillRect(new Rect2I(12, 0, 84, 108), Ink700);
        FillSpeckled(img, new Rect2I(14, 2, 80, 104), Cream, StonePale, 4);
        img.FillRect(new Rect2I(14, 2, 80, 8), StoneShade);       // roof gravel

        // The upswept eave overhanging past the wall — the single strongest era cue.
        img.FillRect(new Rect2I(4, 6, 92, 4), Ink700);
        img.FillRect(new Rect2I(4, 10, 92, 4), NeonAqua);
        img.FillRect(new Rect2I(4, 14, 10, 2), Ink700);

        img.FillRect(new Rect2I(20, 20, 52, 12), Ink900);         // OFFICE box
        PixelFont.DrawCentered(img, 46, 21, "OFFICE", night ? Lamp : NeonAqua, 2);

        // Plate glass: the lobby light is on all night — Walt lives behind that desk.
        img.FillRect(new Rect2I(20, 38, 48, 42), Ink700);
        img.FillRect(new Rect2I(21, 39, 46, 40), night ? GlassNightLit : GlassDay);
        img.FillRect(new Rect2I(44, 39, 1, 40), StonePale);       // mullions
        img.FillRect(new Rect2I(21, 52, 46, 1), StonePale);
        if (!night)
        {
            for (int i = 0; i < 18; i++)
                img.FillRect(new Rect2I(24 + i, 75 - i, 4, 1), Reflection);
        }
        img.FillRect(new Rect2I(20, 80, 48, 2), Cream);           // sill

        img.FillRect(new Rect2I(74, 62, 16, 40), Ink700);         // door
        img.FillRect(new Rect2I(75, 63, 14, 38), BarnRed);
        img.FillRect(new Rect2I(86, 80, 2, 3), Ink900);

        img.FillRect(new Rect2I(0, 74, 10, 28), Ink700);          // soda machine
        img.FillRect(new Rect2I(1, 75, 8, 26), BarnRed);
        img.FillRect(new Rect2I(2, 78, 6, 5), Cream);

        img.FillRect(new Rect2I(14, 102, 80, 4), StoneDark);      // kick plate
    }

    private static void DrawStrip(Image img, bool night, int litRoom)
    {
        img.FillRect(new Rect2I(108, 24, 292, 84), Ink700);
        FillSpeckled(img, new Rect2I(110, 26, 288, 80), Cream, StonePale, 4);
        img.FillRect(new Rect2I(110, 26, 288, 10), StoneShade);   // roof gravel
        img.FillRect(new Rect2I(110, 36, 288, 2), Ink700);        // shadow line
        img.FillRect(new Rect2I(110, 38, 288, 4), NeonAqua);      // googie stripe

        Color[] doors = { BarnRed, NeonAqua, BarnRed, NeonAqua };
        for (int i = 0; i < 4; i++)
        {
            int ux = 110 + i * 68;
            img.FillRect(new Rect2I(ux + 2, 42, 2, 62), NeonAqua);           // canopy post

            img.FillRect(new Rect2I(ux + 4, 62, 18, 40), Ink700);            // door
            img.FillRect(new Rect2I(ux + 5, 63, 16, 38), doors[i]);
            img.FillRect(new Rect2I(ux + 17, 80, 2, 3), Ink900);

            img.FillRect(new Rect2I(ux + 8, 52, 10, 8), Ink900);             // number plaque
            PixelFont.DrawCentered(img, ux + 13, 53, (i + 1).ToString(), Cream);

            bool lit = night && litRoom == i + 1;
            img.FillRect(new Rect2I(ux + 28, 56, 32, 28), Ink700);           // window
            img.FillRect(new Rect2I(ux + 29, 57, 30, 26),
                lit ? GlassNightLit : night ? Ink900 : GlassDay);
            img.FillRect(new Rect2I(ux + 43, 57, 1, 26), StonePale);         // mullions
            img.FillRect(new Rect2I(ux + 29, 69, 30, 1), StonePale);
            img.FillRect(new Rect2I(ux + 28, 84, 32, 2), Cream);             // sill
        }

        // Ice/vending alcove: a cheap ambient night light and a natural hiding place.
        img.FillRect(new Rect2I(370, 58, 24, 44), Ink700);
        img.FillRect(new Rect2I(371, 59, 22, 42), StoneShade);
        img.FillRect(new Rect2I(374, 62, 7, 16), NeonAqua);
        img.FillRect(new Rect2I(384, 62, 7, 16), BarnRed);
        PixelFont.DrawCentered(img, 382, 84, "ICE", Cream);

        img.FillRect(new Rect2I(110, 102, 288, 4), StoneDark);    // kick plate
    }

    /// <summary>Enamel chalking: base colour with a deterministic percentage of
    /// speckle pixels, the handoff's mottle approach.</summary>
    private static void FillSpeckled(Image img, Rect2I rect, Color baseColor, Color speck, int percent)
    {
        img.FillRect(rect, baseColor);
        for (int y = rect.Position.Y; y < rect.End.Y; y++)
        {
            for (int x = rect.Position.X; x < rect.End.X; x++)
            {
                unchecked
                {
                    uint h = (uint)(x * 73856093 ^ y * 19349663);
                    h ^= h >> 13;
                    h *= 2654435761;
                    h ^= h >> 16;
                    if (h % 100 < percent)
                        img.SetPixel(x, y, speck);
                }
            }
        }
    }
}
