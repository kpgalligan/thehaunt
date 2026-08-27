using Godot;

namespace TheHaunt.World;

/// <summary>
/// The generic pole mount (motel handoff §3): a cabinet on a pylon set back from the
/// road, meant to be read from a moving car. The motel's own sign is the bespoke
/// <see cref="MotelSign"/>; this plainer cabinet serves anything else expecting
/// passing traffic — the fireworks stand, the drive-in's dead marquee. Static and
/// unlit: nobody else out here pays to light one. Position is the bottom-centre of
/// the concrete foot, like every <see cref="Prop"/>.
/// </summary>
public partial class PoleSign : Sprite2D
{
    public string[] Lines { get; init; } = Array.Empty<string>();

    public Color Face { get; init; } = new("ede3cb");
    public Color Letters { get; init; } = new("a4432f");

    private static readonly Color Outline = new("2b241d");
    private static readonly Color Pylon = new("575a58");
    private static readonly Color Foot = new("3e4241");

    public override void _Ready()
    {
        ImageTexture texture = Build();
        Texture = texture;
        Offset = new Vector2(0, -texture.GetHeight() / 2f);
    }

    private ImageTexture Build()
    {
        const int scale = 1;
        int textW = 0;
        foreach (string line in Lines)
            textW = Mathf.Max(textW, PixelFont.Measure(line, scale));

        int cabinetW = Mathf.Max(textW + 12, 34);
        int lineH = PixelFont.GlyphHeight * scale + 4;
        int cabinetH = Lines.Length * lineH + 6;
        const int pylonH = 12, footH = 4;

        int w = cabinetW + 4;
        int h = cabinetH + 4 + pylonH + footH;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        int cx = w / 2;
        img.FillRect(new Rect2I(0, 0, cabinetW + 4, cabinetH + 4), Outline);
        img.FillRect(new Rect2I(2, 2, cabinetW, cabinetH), Face);
        for (int i = 0; i < Lines.Length; i++)
            PixelFont.DrawCentered(img, cx, 5 + i * lineH, Lines[i], Letters, scale);

        img.FillRect(new Rect2I(cx - 4, cabinetH + 4, 8, pylonH), Outline);
        img.FillRect(new Rect2I(cx - 3, cabinetH + 4, 6, pylonH), Pylon);
        img.FillRect(new Rect2I(cx - 8, h - footH, 16, footH), Foot);
        return ImageTexture.CreateFromImage(img);
    }
}
