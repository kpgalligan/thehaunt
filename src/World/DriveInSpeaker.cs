using Godot;

namespace TheHaunt.World;

/// <summary>
/// A drive-in speaker post: waist-high pipe, the speaker box hanging off it, its
/// cable long since perished. One tile, base-anchored, blocked by the map's Blocker
/// cell. Placeholder grammar, like the screen.
/// </summary>
public partial class DriveInSpeaker : Sprite2D
{
    private static readonly Color Pipe = new("575a58");
    private static readonly Color Box = new("3e4241");
    private static readonly Color Grille = new("7a7a7a");

    public override void _Ready()
    {
        const int w = 12, h = 22;
        Offset = new Vector2(0, -h / 2f);

        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        img.FillRect(new Rect2I(5, 6, 2, 16), Pipe);      // the post
        img.FillRect(new Rect2I(2, 0, 8, 8), Box);        // the box
        img.FillRect(new Rect2I(4, 2, 4, 1), Grille);
        img.FillRect(new Rect2I(4, 4, 4, 1), Grille);
        Texture = ImageTexture.CreateFromImage(img);
    }
}
