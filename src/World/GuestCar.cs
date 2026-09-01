using Godot;

namespace TheHaunt.World;

/// <summary>
/// A guest's car in the motor court's lot — one per occupied room
/// (<see cref="TheHaunt.Core.MotelRules.OccupiedRooms"/>): a guest at a roadside
/// motel arrived in something, and a lot with a lit room and no car reads wrong.
/// A VIEW in the PlaceholderBuilding tradition: the sedan is drawn in code until
/// real art lands, side elevation, base-anchored on its footprint's bottom-centre
/// (3 tiles wide, 1 deep), nose west like a car backed up to its door. It owns its
/// blocker (the lot is asphalt — there are no obstacle cells to borrow), and holds
/// nothing durable: WestEntryMap.ApplyState diffs the set from the model.
/// </summary>
public partial class GuestCar : Node2D
{
    private const int ImageWidth = 48;
    private const int ImageHeight = 20;

    /// <summary>Body paint; trim and glass are derived shades.</summary>
    public Color Paint { get; init; } = new("5c6a76");

    public override void _Ready()
    {
        AddChild(new Sprite2D
        {
            Texture = Build(),
            Offset = new Vector2(0, -ImageHeight / 2f),
        });

        // Solid to the player over the base row only, so walking behind the cabin
        // still Y-sorts as depth rather than bumping an invisible wall.
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Position = new Vector2(0, -7),
            Shape = new RectangleShape2D { Size = new Vector2(44, 12) },
        });
        AddChild(blocker);
    }

    private ImageTexture Build()
    {
        Color roofLight = Paint.Lightened(0.18f);
        Color skirt = Paint.Darkened(0.35f);
        Color glass = new("2f3a42");
        Color tyre = new("15130f");
        Color hub = new("6a685c");
        Color headlamp = new("d8d4b0");
        Color taillight = new("7a3028");

        var img = Image.CreateEmpty(ImageWidth, ImageHeight, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Body band, corners knocked off; nose at x1, tail at x46.
        img.FillRect(new Rect2I(1, 9, 46, 6), Paint);
        img.FillRect(new Rect2I(2, 8, 44, 1), Paint);
        img.FillRect(new Rect2I(2, 15, 44, 1), skirt);

        // Cabin and glass — two windows, one pillar, a light catch on the roof.
        img.FillRect(new Rect2I(12, 3, 23, 6), Paint);
        img.FillRect(new Rect2I(13, 2, 21, 1), roofLight);
        img.FillRect(new Rect2I(14, 4, 9, 4), glass);
        img.FillRect(new Rect2I(25, 4, 9, 4), glass);

        // Lamps at the corners of the band.
        img.FillRect(new Rect2I(1, 9, 2, 2), headlamp);
        img.FillRect(new Rect2I(45, 9, 2, 2), taillight);

        // Wheels: tyre discs with a hubcap pixel, sitting proud of the skirt.
        foreach (int cx in new[] { 10, 37 })
        {
            img.FillRect(new Rect2I(cx - 3, 13, 7, 6), tyre);
            img.FillRect(new Rect2I(cx - 2, 12, 5, 1), tyre);
            img.FillRect(new Rect2I(cx - 1, 15, 2, 2), hub);
        }

        return ImageTexture.CreateFromImage(img);
    }
}
