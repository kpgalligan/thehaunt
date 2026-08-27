using Godot;

namespace TheHaunt.World;

/// <summary>
/// The chain across a road that is closed: two posts and a sagging run of links,
/// built in code until the prop is drawn. Visual only — the owning map paints Blocker
/// cells under its span, the same split every facade uses — so it reads as a barrier
/// instead of an invisible wall. Base-anchored like every drawn thing.
/// </summary>
public partial class RoadBarrier : Sprite2D
{
    public int TilesWide { get; init; } = 2;

    public override void _Ready()
    {
        int w = TilesWide * MapRoot.TileSize;
        const int h = 16;
        Offset = new Vector2(0, -h / 2f);
        Texture = Build(w, h);
    }

    private static ImageTexture Build(int w, int h)
    {
        var post = new Color("5a4a3a");
        var cap = new Color("6b5a45");
        var chain = new Color("62625a");

        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);

        img.FillRect(new Rect2I(1, 3, 3, h - 3), post);
        img.FillRect(new Rect2I(w - 4, 3, 3, h - 3), post);
        img.FillRect(new Rect2I(1, 3, 3, 1), cap);
        img.FillRect(new Rect2I(w - 4, 3, 3, 1), cap);

        // The chain sags a couple of pixels at mid-span; every other pixel skipped so
        // it reads as links rather than a rope.
        int span = w - 8;
        for (int i = 0; i <= span; i++)
        {
            if (i % 2 == 1)
                continue;
            float u = (float)i / span;
            int sag = (int)(4f * u * (1f - u) * 3f);
            img.SetPixel(4 + i, 5 + sag, chain);
        }

        return ImageTexture.CreateFromImage(img);
    }
}
