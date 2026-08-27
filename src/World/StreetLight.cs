using Godot;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// An aluminium cobra-head street light on a tapered pole (motel handoff §Street
/// lights): mast arm, luminaire, and cold mercury-vapour light — a directed CONE
/// down to the road plus a ground pool, never a warm radial. The town electrified
/// before it had taste, and the three light sources never mix: neon on signs, amber
/// strictly indoors, mercury vapour on the street.
///
/// A dead head stays dead — not flickering, dead — so the motel sign's V remains the
/// only animated thing in the game. Position is the bottom-centre of the pole base;
/// the map blocks the base cell.
/// </summary>
public partial class StreetLight : Sprite2D
{
    public bool Lit { get; init; } = true;

    /// <summary>True to hang the luminaire to the west instead of the east.</summary>
    public bool ArmLeft { get; init; }

    private const int W = 38, H = 64;
    private const int PoleX = 4;           // pole silhouette local x (arm-right layout)
    private const int TopY = 2;

    private static readonly Color Silhouette = new("2b241d");
    private static readonly Color PoleBody = new("575a58");
    private static readonly Color PoleHighlight = new("9a9a8a");
    private static readonly Color Collar = new("171310");
    private static readonly Color Shell = new("b8b5a5");
    private static readonly Color ShellInner = new("9a9a8a");
    private static readonly Color Underside = new("171310");
    private static readonly Color LensLit = new("ede3cb");
    private static readonly Color LensDead = new("3e4241");

    // Mercury vapour — cold blue-green, deliberately outside the warm palette.
    private static readonly Color Mercury = new(175 / 255f, 230 / 255f, 225 / 255f);
    private static readonly Color ConeTop = new(190 / 255f, 235 / 255f, 230 / 255f);
    private static readonly Color ConeBottom = new(130 / 255f, 205 / 255f, 205 / 255f);

    private Sprite2D? _cone;

    public override void _Ready()
    {
        Image img = BuildHead(Lit);
        float baseX = PoleX + 1.5f;          // pole base centre in texture space
        float lensX = PoleX + 24.5f;         // lens centre
        if (ArmLeft)
        {
            img.FlipX();
            baseX = W - baseX;
            lensX = W - lensX;
        }
        Texture = ImageTexture.CreateFromImage(img);
        Offset = new Vector2(W / 2f - baseX, -H / 2f);

        if (!Lit)
            return;

        // The lens throws a directed pattern: the cone IS the cobra-head read.
        float lensDx = lensX - baseX;
        _cone = BuildCone();
        _cone.Position = new Vector2(lensDx, -(H - (TopY + 10)));
        AddChild(_cone);

        AddChild(new GlowLight
        {
            Name = "Pool",
            Size = GlowLight.Falloff.Large,
            Strength = 0.5f,
            Color = Mercury,
            Position = new Vector2(lensDx, 16),  // the ground pool, out on the road
        });
    }

    public override void _Process(double delta)
    {
        if (_cone == null)
            return;
        float level = DayNight.LightLevel(Clock.Instance.Now);
        _cone.Visible = level > 0.001f;
        _cone.Modulate = new Color(1, 1, 1, level);
    }

    private static Image BuildHead(bool lit)
    {
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Tapered pole with one highlight column so it reads against asphalt.
        img.FillRect(new Rect2I(PoleX - 1, TopY + 5, 5, 55), Silhouette);
        img.FillRect(new Rect2I(PoleX, TopY + 5, 3, 55), PoleBody);
        img.FillRect(new Rect2I(PoleX + 1, TopY + 5, 1, 55), PoleHighlight);
        img.FillRect(new Rect2I(PoleX - 3, H - 4, 9, 4), Collar);

        // The mast arm arches out to the head.
        foreach ((int dx, int dy) in new[] { (0, 5), (3, 3), (6, 2), (9, 2), (12, 2), (15, 3), (18, 4) })
        {
            img.FillRect(new Rect2I(PoleX + dx, TopY + dy, 4, 4), Silhouette);
            img.FillRect(new Rect2I(PoleX + dx, TopY + dy + 1, 3, 2), PoleBody);
        }

        // The luminaire. No finials, no scrollwork, nothing cast-iron.
        img.FillRect(new Rect2I(PoleX + 16, TopY + 2, 17, 9), Silhouette);
        img.FillRect(new Rect2I(PoleX + 17, TopY + 3, 15, 4), Shell);
        img.FillRect(new Rect2I(PoleX + 18, TopY + 4, 13, 1), ShellInner);
        img.FillRect(new Rect2I(PoleX + 17, TopY + 7, 15, 3), Underside);
        img.FillRect(new Rect2I(PoleX + 20, TopY + 8, 9, 2), lit ? LensLit : LensDead);
        return img;
    }

    private Sprite2D BuildCone()
    {
        const int coneW = 34, coneH = 44;
        var img = Image.CreateEmpty(coneW, coneH, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        for (int y = 0; y < coneH; y++)
        {
            float t = (float)y / (coneH - 1);
            float half = Mathf.Lerp(3f, coneW / 2f - 1f, t);
            Color c = ConeTop.Lerp(ConeBottom, t);
            c.A = Mathf.Lerp(0.34f, 0f, t);
            int cx = coneW / 2;
            for (int x = (int)(cx - half); x <= cx + half; x++)
                img.SetPixel(x, y, c);
        }
        return new Sprite2D
        {
            Name = "Cone",
            Texture = ImageTexture.CreateFromImage(img),
            Offset = new Vector2(0, coneH / 2f),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
    }
}
