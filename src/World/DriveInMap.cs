using Godot;
using TheHaunt.Content;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The drive-in theater, 30x24 tiles, off the south side of the road in the east
/// fork's frame (src/Content/Places): the screen tower at the far end, a cracked
/// asphalt field ramped for cars, speaker posts with their cables perished, a bench
/// row for people who came without cars, and the boarded concession stand. It shut
/// down years ago and nothing here works; Jane's long-running goal of refurbishing
/// and reopening it comes later — no flag moves any of this yet.
///
/// All placeholder-grammar art (screen, speakers, stand), on the roadside asphalt
/// the motel handoff introduced. Entered from the north, down the drive.
/// </summary>
public partial class DriveInMap : ExteriorMap
{
    private const int Width = 30;
    private const int Height = 24;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    // The drive in from the road (north edge), the lot, and the screen at the south.
    private const int DriveLeft = 14, DriveRight = 15;
    private const int LotLeft = 4, LotTop = 8, LotRight = 25, LotBottom = 19;
    private const int StandLeft = 18, StandTop = 4, StandRight = 22, StandBottom = 6;
    private const int ScreenLeft = 8, ScreenRight = 21, ScreenRow = 22;
    private static readonly Vector2I Marquee = new(10, 4);

    private static readonly Vector2I[] Speakers =
    {
        new(6, 10), new(11, 10), new(16, 10), new(21, 10),
        new(6, 14), new(11, 14), new(16, 14), new(21, 14),
    };

    private static readonly int[] BenchX = { 9, 13, 17 };
    private const int BenchRow = 18;

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.DriveIn;
        base._EnterTree();
    }

    // No paved road here: the drive in is dirt, and the field is kerbless asphalt.
    protected override bool HasRoad => false;

    protected override void BuildDefaultSurfaces()
    {
        ResetSurfaces();

        // The drive pierces the north treeline down to the field.
        Fill(DriveLeft, 0, DriveRight, LotTop - 1, Surface.Dirt);
        Fill(LotLeft, LotTop, LotRight, LotBottom, Surface.Asphalt);

        // Gravel under the concession stand and one apron row.
        Fill(StandLeft, StandTop, StandRight, StandBottom + 1, Surface.Gravel);
    }

    /// <summary>The drive-in's placements as the C# literals describe them: the seed
    /// <c>data/maps/drive_in.tmx</c> was written from, and the fallback when it is missing.</summary>
    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.DriveIn);

        recipe.Add(PlacementKinds.Prop, "screen", ScreenLeft, ScreenRow);
        recipe.Add(PlacementKinds.Prop, "concession", StandLeft, StandBottom);
        recipe.Add(PlacementKinds.Prop, "marquee", Marquee.X, Marquee.Y);
        foreach (Vector2I speaker in Speakers)
            recipe.Add(PlacementKinds.Prop, "speaker", speaker.X, speaker.Y);
        foreach (int x in BenchX)
            recipe.Add(PlacementKinds.Prop, x % 2 == 0 ? TownProps.BenchBId : TownProps.BenchAId, x, BenchRow);

        // >= 1 tile clear of the exit area (spawn-clearance rule).
        recipe.Add(PlacementKinds.Spawn, "from_road", 14, 3);
        recipe.Add(PlacementKinds.Spawn, "default", 15, 5);

        // The marquee's read area rides its foot tile; the art draws it. Copy lives
        // with the place (src/Content/Places/DriveIn.cs).
        recipe.Add(PlacementKinds.Sign, "MarqueeRead", Marquee.X, Marquee.Y)
            .SetBool(PlacementFields.Board, false);

        // 2x1, wider than deep — see the east fork's drive-in mouth.
        MapPlacement exit = recipe.Add(PlacementKinds.Exit, MapIds.EastFork, DriveLeft, 0);
        exit.SetText(PlacementFields.Spawn, "from_drive_in");
        exit.SetInt(PlacementFields.Width, 2);
        exit.SetInt(PlacementFields.Height, 1);

        return recipe;
    }

    protected override string? SignTextFor(string signId) => DriveIn.SignTextFor(signId);

    protected override IReadOnlyDictionary<string, ExteriorProp> PropCatalog() =>
        new Dictionary<string, ExteriorProp>(StringComparer.Ordinal)
        {
            // The screen's legs. The walkable strip behind them, under the drawn face,
            // stays open on purpose.
            ["screen"] = new(p => new DriveInScreen
            {
                Name = "Screen",
                TilesWide = ScreenRight - ScreenLeft + 1,
                Position = Prop.Anchor(p.X, p.Y, ScreenRight - ScreenLeft + 1),
            }, ExteriorProp.Rows(ScreenRight - ScreenLeft + 1, 2)),
            // No door gap: the stand is boarded, and the boards are the whole answer.
            ["concession"] = new(BuildConcession,
                ExteriorProp.Rows(StandRight - StandLeft + 1, StandBottom - StandTop + 1)),
            // The marquee: the pole mount, dead. The nameplate question is Kevin's —
            // the board carries only what the location is and the state it is in. Its
            // read area (MarqueeRead) carries the blocker.
            ["marquee"] = new(p => new PoleSign
            {
                Name = "Marquee",
                Lines = new[] { "DRIVE-IN", "CLO ED" },
                Face = new Color("b8b5a5"),
                Letters = new Color("453a2e"),
                Position = Prop.Anchor(p.X, p.Y),
            }),
            ["speaker"] = new(p => new DriveInSpeaker { Position = Prop.Anchor(p.X, p.Y) },
                ExteriorProp.Rows(1, 1)),
            [TownProps.BenchAId] = new(p => Bench(p, TownProps.BenchA), ExteriorProp.Rows(2, 1)),
            [TownProps.BenchBId] = new(p => Bench(p, TownProps.BenchB), ExteriorProp.Rows(2, 1)),
        };

    private static Node2D BuildConcession(MapPlacement p)
    {
        var stand = new PlaceholderBuilding
        {
            Name = "Concession",
            TilesWide = StandRight - StandLeft + 1,
            FootprintRows = StandBottom - StandTop + 1,
            Wall = new Color("8a8578"),
            Boarded = true,
            Position = Prop.Anchor(p.X, p.Y, StandRight - StandLeft + 1),
        };
        stand.AddChild(new WallBandSign
        {
            Text = "SNACKS",
            LitAtNight = false,
            Position = new Vector2(0, -57),
        });
        return stand;
    }

    private static Prop Bench(MapPlacement p, Rect2 source) => new()
    {
        Name = $"Bench{p.X}",
        TexturePath = TownProps.TexturePath,
        Source = source,
        Position = Prop.Anchor(p.X, p.Y, 2),
    };

    protected override void BuildDressing(TileMapLayer ground)
    {
        if (FieldRamps is { } ramps)
            ground.AddChild(BuildFieldMarkings(ramps.Field, ramps.RampRowsPx));
    }

    // ------------------------------------------------------------------
    // Field dressing: ramp wear, cracks, and the weeds that win in the end.
    // Flat decal child of the Ground layer, same contract as the motel lot's.
    // ------------------------------------------------------------------

    private static readonly Color RampLine = new("b8b5a5");
    private static readonly Color Crack = new("3e4241");
    private static readonly Color WeedDark = new("2f5228");
    private static readonly Color Weed = new("457539");

    private static readonly int[] RampRowsPx = { 40, 104, 168 };

    /// <summary>The asphalt field (tiles — the bounding box of the map's Asphalt) and its
    /// three ramp rows, as field-local pixel rows. Read-only, for the world dump and the
    /// field; null before the build, or when no Asphalt is painted.</summary>
    internal (Rect2I Field, IReadOnlyList<int> RampRowsPx)? FieldRamps =>
        BoundsOf(Surface.Asphalt) is { } field ? (field, RampRowsPx) : null;

    private static Sprite2D BuildFieldMarkings(Rect2I field, IReadOnlyList<int> rampRowsPx)
    {
        int w = field.Size.X * TileSize;   // 352
        int h = field.Size.Y * TileSize;   // 192
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Three ramp rows, the paint nearly gone: broken dashes, not lines.
        foreach (int y in rampRowsPx)
        {
            for (int x = 4; x < w - 12; x += 22)
                img.FillRect(new Rect2I(x + Hash(x, y) % 6, y, 9, 1), RampLine);
        }

        // Years of frost heave.
        for (int i = 0; i < 80; i++)
        {
            int cx = Hash(i, 3) % (w - 8);
            int cy = Hash(i, 7) % (h - 4);
            int len = 2 + Hash(i, 11) % 7;
            bool vertical = Hash(i, 13) % 4 == 0;
            img.FillRect(vertical ? new Rect2I(cx, cy, 1, len) : new Rect2I(cx, cy, len, 1), Crack);
        }

        // Weeds through the cracks, thickest at the edges of the field.
        for (int i = 0; i < 70; i++)
        {
            int cx = Hash(i, 17) % (w - 4);
            int cy = Hash(i, 19) % (h - 4);
            bool edge = cx < 40 || cx > w - 44 || cy > h - 40;
            if (!edge && Hash(i, 23) % 3 != 0)
                continue;
            img.FillRect(new Rect2I(cx, cy + 1, 3, 1), WeedDark);
            img.FillRect(new Rect2I(cx + 1, cy, 1, 2), Weed);
        }

        return new Sprite2D
        {
            Name = "FieldMarkings",
            Centered = false,
            Position = new Vector2(field.Position.X * TileSize, field.Position.Y * TileSize),
            Texture = ImageTexture.CreateFromImage(img),
        };
    }
}
