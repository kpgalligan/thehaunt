using Godot;

namespace TheHaunt.World;

/// <summary>
/// The covered pit outside Billie's: heavy planks laid over a hole nobody talks about,
/// a strip of dark showing between them. Built in code until the prop is drawn. The
/// owning map paints Blocker cells under the whole cover — nothing stands on it.
/// Base-anchored on its bottom row like every drawn thing.
/// </summary>
public partial class PitCover : Sprite2D
{
    public const int TilesWide = 3;
    public const int TilesTall = 2;

    public override void _Ready()
    {
        const int w = TilesWide * MapRoot.TileSize;
        const int h = TilesTall * MapRoot.TileSize;
        Offset = new Vector2(0, -h / 2f);
        Texture = Build(w, h);
    }

    private static ImageTexture Build(int w, int h)
    {
        var dark = new Color("241f1a");
        var plank = new Color("6b5a45");
        var plankWorn = new Color("5f5040");
        var rim = new Color("3a3a34");

        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);

        // The rim of the hole, and the dark under the boards.
        img.FillRect(new Rect2I(0, 0, w, h), rim);
        img.FillRect(new Rect2I(2, 2, w - 4, h - 4), dark);

        // Planks laid across, one gap left showing the drop.
        for (int row = 0; row < 5; row++)
        {
            if (row == 2)
                continue;
            img.FillRect(new Rect2I(1, 3 + row * 6, w - 2, 4), row % 2 == 0 ? plank : plankWorn);
        }

        return ImageTexture.CreateFromImage(img);
    }
}
