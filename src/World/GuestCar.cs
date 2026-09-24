using Godot;

namespace TheHaunt.World;

/// <summary>
/// A late-50s sedan, drawn in code until real art lands (a VIEW in the
/// PlaceholderBuilding tradition): a guest's car in the motor court's lot — one per
/// occupied room (<see cref="TheHaunt.Core.MotelRules.OccupiedRooms"/>): a guest at a
/// roadside motel arrived in something, and a lot with a lit room and no car reads
/// wrong — and the customer cars on the garage's lifts. Base-anchored on its
/// footprint's bottom-centre; owns its blocker (the lot is asphalt — there are no
/// obstacle cells to borrow); holds nothing durable: WestEntryMap.ApplyState (and the
/// garage's) diff the set from the model.
///
/// Two views of the same car. <see cref="NoseIn"/> (the lot, Kevin 2026-09-24): parked
/// nose-in facing the motel (north) between its stall's stripes, so the camera, which
/// looks north, sees the REAR — fins with tail lamps, chrome bumper, a blank plate,
/// the rear window and roof, the hood beyond — on a footprint 24 px wide and
/// <see cref="RearDepth"/> (two tiles) long. Otherwise (the garage lifts, side-on on
/// their 3-tile rails): the side elevation, nose west, 3 tiles wide, 1 deep.
/// </summary>
public partial class GuestCar : Node2D
{
    private const int SideWidth = 48;
    private const int SideHeight = 20;
    private const int RearWidth = 24;
    private const int RearHeight = 34;

    /// <summary>The nose-in car's footprint length along the stall (px): two tiles,
    /// the drawn rear view rising 2 px above it.</summary>
    internal const int RearDepth = 32;

    /// <summary>Parked nose-in, facing north (the camera sees its rear); false = the
    /// side elevation, nose west.</summary>
    public bool NoseIn { get; init; }

    /// <summary>Body paint; trim and glass are derived shades.</summary>
    public Color Paint { get; init; } = new("5c6a76");

    /// <summary>Which way the nose points: N (nose-in) or W (side elevation).</summary>
    internal string Facing => NoseIn ? "N" : "W";

    /// <summary>The ground the car stands on, relative to its Position (the footprint's
    /// bottom-centre), in px.</summary>
    internal Rect2 FootprintPx => NoseIn
        ? new Rect2(-RearWidth / 2f, -RearDepth, RearWidth, RearDepth)
        : new Rect2(-SideWidth / 2f, -MapRoot.TileSize, SideWidth, MapRoot.TileSize);

    /// <summary>The blocker's rect relative to Position (px).</summary>
    internal Rect2 BlockerPx => NoseIn
        // The whole footprint, a pixel in at each side: the drawn car IS its ground
        // (the rear face, then the deck, roof and hood running north), so the player
        // walks round it, and passing north of the nose still Y-sorts behind it.
        ? new Rect2(-RearWidth / 2f + 1, -RearDepth + 1, RearWidth - 2, RearDepth - 1)
        // Solid over the base row only, so walking behind the cabin still Y-sorts
        // as depth rather than bumping an invisible wall.
        : new Rect2(-22, -13, 44, 12);

    public override void _Ready()
    {
        int height = NoseIn ? RearHeight : SideHeight;
        AddChild(new Sprite2D
        {
            Texture = NoseIn ? BuildRear() : BuildSide(),
            Offset = new Vector2(0, -height / 2f),
        });

        Rect2 b = BlockerPx;
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Position = b.GetCenter(),
            Shape = new RectangleShape2D { Size = b.Size },
        });
        AddChild(blocker);
    }

    private ImageTexture BuildSide()
    {
        Color roofLight = Paint.Lightened(0.18f);
        Color skirt = Paint.Darkened(0.35f);
        Color glass = new("2f3a42");
        Color tyre = new("15130f");
        Color hub = new("6a685c");
        Color headlamp = new("d8d4b0");
        Color taillight = new("7a3028");

        var img = Image.CreateEmpty(SideWidth, SideHeight, false, Image.Format.Rgba8);
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

    /// <summary>The rear view, 24x34, bottom row on the ground: read bottom-up it is
    /// the rear face (tyres, bumper, plate, fins and tail lamps), then the tops running
    /// away north (trunk deck, rear window, roof, a sliver of windscreen, the hood).
    /// The same palette as the side view.</summary>
    private ImageTexture BuildRear()
    {
        Color roofLight = Paint.Lightened(0.18f);
        Color deck = Paint.Lightened(0.08f);
        Color flank = Paint.Darkened(0.18f);
        Color skirt = Paint.Darkened(0.35f);
        Color glass = new("2f3a42");
        Color glint = new("4a5862");
        Color tyre = new("15130f");
        Color under = new("2a2824");
        Color chrome = new("9c9a8c");
        Color chromeLight = new("c4c1b0");
        Color chromeDark = new("6a685c");
        Color plate = new("c8c3a4");
        Color taillight = new("7a3028");
        Color lamp = new("a4463a");

        var img = Image.CreateEmpty(RearWidth, RearHeight, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Tyres at the corners, the shadowed underside between them.
        img.FillRect(new Rect2I(1, 29, 4, 5), tyre);
        img.FillRect(new Rect2I(19, 29, 4, 5), tyre);
        img.FillRect(new Rect2I(5, 30, 14, 1), under);

        // The body's run north: flanks either side of the cabin, the hood beyond it.
        img.FillRect(new Rect2I(1, 0, 22, 18), Paint);
        img.FillRect(new Rect2I(1, 1, 1, 17), flank);
        img.FillRect(new Rect2I(22, 1, 1, 17), flank);
        img.FillRect(new Rect2I(2, 0, 20, 1), flank);     // the nose's edge, far off

        // The cabin: windscreen sliver, roof with its light catch, rear window.
        img.FillRect(new Rect2I(5, 2, 14, 2), glass);
        img.FillRect(new Rect2I(4, 4, 16, 5), Paint);
        img.FillRect(new Rect2I(6, 5, 12, 3), roofLight);
        img.FillRect(new Rect2I(6, 9, 12, 1), glass);
        img.FillRect(new Rect2I(5, 10, 14, 5), glass);
        img.FillRect(new Rect2I(7, 11, 1, 1), glint);
        img.FillRect(new Rect2I(8, 10, 1, 1), glint);

        // Trunk deck, and the fins rising either side of it.
        img.FillRect(new Rect2I(2, 15, 20, 3), deck);
        img.FillRect(new Rect2I(0, 12, 2, 13), Paint);
        img.FillRect(new Rect2I(22, 12, 2, 13), Paint);
        img.FillRect(new Rect2I(0, 12, 2, 1), roofLight);
        img.FillRect(new Rect2I(22, 12, 2, 1), roofLight);

        // The rear face: chrome lip, the tail lamps in the fin ends, trunk lock,
        // a blank plate (no lettering: PixelFont is the only typeface, and the car
        // carries none), its lower edge shading into the bumper.
        img.FillRect(new Rect2I(2, 18, 20, 8), Paint);
        img.FillRect(new Rect2I(3, 18, 18, 1), chromeLight);
        img.FillRect(new Rect2I(0, 15, 2, 7), taillight);
        img.FillRect(new Rect2I(22, 15, 2, 7), taillight);
        img.FillRect(new Rect2I(0, 17, 1, 3), lamp);
        img.FillRect(new Rect2I(23, 17, 1, 3), lamp);
        img.FillRect(new Rect2I(11, 20, 2, 1), chrome);
        img.FillRect(new Rect2I(8, 21, 8, 5), skirt);
        img.FillRect(new Rect2I(9, 22, 6, 3), plate);
        img.FillRect(new Rect2I(2, 25, 6, 1), skirt);
        img.FillRect(new Rect2I(16, 25, 6, 1), skirt);

        // The bumper, full width, with two overriders.
        img.FillRect(new Rect2I(0, 26, 24, 1), chromeLight);
        img.FillRect(new Rect2I(0, 27, 24, 2), chrome);
        img.FillRect(new Rect2I(1, 29, 22, 1), chromeDark);
        img.FillRect(new Rect2I(6, 24, 1, 5), chromeLight);
        img.FillRect(new Rect2I(17, 24, 1, 5), chromeLight);

        return ImageTexture.CreateFromImage(img);
    }
}
