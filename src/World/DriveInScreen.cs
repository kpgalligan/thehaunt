using Godot;

namespace TheHaunt.World;

/// <summary>
/// The drive-in's screen tower: a big weathered white face on timber legs, drawn
/// front-face-only in elevation like every vertical thing in town, base-anchored.
/// It shut down years ago — the surface is chalked grey, streaked with water stains,
/// and one top corner of the panelling has let go. Procedural, in placeholder
/// grammar: the day this gets real art it becomes a Prop at the same Position.
/// </summary>
public partial class DriveInScreen : Sprite2D
{
    /// <summary>Footprint width in tiles; the face fills it.</summary>
    public int TilesWide { get; init; } = 14;

    private const int FaceHeight = 56;
    private const int LegHeight = 16;

    private static readonly Color Casing = new("2b241d");
    private static readonly Color Screen = new("ede3cb");
    private static readonly Color Chalk = new("b8b5a5");
    private static readonly Color Stain = new("9a9a8a");
    private static readonly Color Timber = new("453a2e");

    public override void _Ready()
    {
        int w = TilesWide * MapRoot.TileSize;
        int h = FaceHeight + LegHeight;
        Offset = new Vector2(0, -h / 2f);
        Texture = Build(w, h);
    }

    private static Texture2D Build(int w, int h)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Legs first, so the face reads as standing on them.
        int legY = FaceHeight - 4;
        foreach (int legX in new[] { w / 6, w / 2 - 2, w - w / 6 - 4 })
            img.FillRect(new Rect2I(legX, legY, 4, h - legY), Timber);

        img.FillRect(new Rect2I(0, 0, w, FaceHeight), Casing);
        img.FillRect(new Rect2I(3, 3, w - 6, FaceHeight - 6), Screen);

        // Chalking and long water stains, deterministic.
        for (int y = 3; y < FaceHeight - 3; y++)
        {
            for (int x = 3; x < w - 3; x++)
            {
                unchecked
                {
                    uint roll = (uint)(x * 73856093 ^ y * 19349663);
                    roll ^= roll >> 13;
                    roll *= 2654435761;
                    roll ^= roll >> 16;
                    if (roll % 100 < 8)
                        img.SetPixel(x, y, Chalk);
                }
            }
        }
        for (int i = 0; i < 6; i++)
        {
            int sx = 10 + i * (w - 20) / 6 + (i * 7) % 9;
            int len = 10 + (i * 13) % (FaceHeight - 18);
            img.FillRect(new Rect2I(sx, 3, 1, len), Stain);
        }

        // The panel that let go, top-right: bare frame showing through.
        img.FillRect(new Rect2I(w - 26, 3, 23, 9), Casing);
        img.FillRect(new Rect2I(w - 26, 12, 12, 4), Casing);

        return ImageTexture.CreateFromImage(img);
    }
}
