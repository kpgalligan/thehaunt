using Godot;

namespace TheHaunt.World;

/// <summary>
/// One two-post car lift on the garage's shop floor — a code-drawn placeholder in
/// the GuestCar/PlaceholderBuilding tradition: side elevation, base-anchored on
/// its 3-tile footprint's bottom-centre, drawn UNDER the car (same container,
/// added first, so the car's sprite wins the tie). Holds nothing durable; the
/// map's ApplyState decides whether a car sits on it.
/// </summary>
public partial class CarLift : Node2D
{
    private const int ImageWidth = 48;
    private const int ImageHeight = 26;

    public override void _Ready()
    {
        AddChild(new Sprite2D
        {
            Texture = Build(),
            Offset = new Vector2(0, -ImageHeight / 2f),
        });
        // No blocker of its own: the map Block()s the lift cells permanently, so
        // an arriving car can never materialize a body on top of the player.
    }

    private static ImageTexture Build()
    {
        var steel = new Color("575a58");
        var steelDark = new Color("3e4241");
        var rail = new Color("6a685c");

        var img = Image.CreateEmpty(ImageWidth, ImageHeight, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Two posts with foot plates, and the runway rails between them sitting
        // just off the floor — the resting position; nothing here animates.
        foreach (int px in new[] { 0, 44 })
        {
            img.FillRect(new Rect2I(px, 0, 4, 24), steel);
            img.FillRect(new Rect2I(px, 0, 1, 24), steelDark);
            img.FillRect(new Rect2I(px == 0 ? 0 : 42, 24, 6, 2), steelDark);
        }
        img.FillRect(new Rect2I(3, 19, 42, 3), rail);
        img.FillRect(new Rect2I(3, 21, 42, 1), steelDark);

        return ImageTexture.CreateFromImage(img);
    }
}
