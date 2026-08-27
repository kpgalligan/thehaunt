using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The drive-in theater, 30x24 tiles, off the south side of the road in the east
/// fork's frame (docs/story/README.md): the screen tower at the far end, a cracked
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

    public override void _Ready()
    {
        BuildSurfaces();
        TileSet tileSet = RoadsideTerrain.Get(); // the field needs the asphalt source
        TileMapLayer ground = BuildGround(tileSet);
        ground.AddChild(BuildFieldMarkings());
        BuildObstacles(tileSet);
        BuildStructures();
        BuildSpawns();
        BuildInteractables();
        BuildTravel();
    }

    private void BuildSurfaces()
    {
        ResetSurfaces();

        // The drive pierces the north treeline down to the field.
        Fill(DriveLeft, 0, DriveRight, LotTop - 1, Surface.Dirt);
        Fill(LotLeft, LotTop, LotRight, LotBottom, Surface.Asphalt);

        // Gravel under the concession stand and one apron row.
        Fill(StandLeft, StandTop, StandRight, StandBottom + 1, Surface.Gravel);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        // No door gap: the stand is boarded, and the boards are the whole answer.
        Block(obstacles, StandLeft, StandTop, StandRight, StandBottom);
        // The screen's legs. The walkable strip behind them, under the drawn face,
        // stays open on purpose.
        Block(obstacles, ScreenLeft, ScreenRow - 1, ScreenRight, ScreenRow);
        foreach (Vector2I speaker in Speakers)
            obstacles.SetCell(speaker, 0, TerrainTiles.Blocker);
        foreach (int x in BenchX)
            Block(obstacles, x, BenchRow, x + 1, BenchRow);
        AddChild(obstacles);
    }

    private void BuildStructures()
    {
        AddChild(new DriveInScreen
        {
            Name = "Screen",
            TilesWide = ScreenRight - ScreenLeft + 1,
            Position = Prop.Anchor(ScreenLeft, ScreenRow, ScreenRight - ScreenLeft + 1),
        });

        var stand = new PlaceholderBuilding
        {
            Name = "Concession",
            TilesWide = StandRight - StandLeft + 1,
            FootprintRows = StandBottom - StandTop + 1,
            Wall = new Color("8a8578"),
            Boarded = true,
            Position = Prop.Anchor(StandLeft, StandBottom, StandRight - StandLeft + 1),
        };
        stand.AddChild(new WallBandSign
        {
            Text = "SNACKS",
            LitAtNight = false,
            Position = new Vector2(0, -57),
        });
        AddChild(stand);

        // The marquee: the pole mount, dead. The nameplate question is Kevin's —
        // the board carries only what the location is and the state it is in.
        AddChild(new PoleSign
        {
            Name = "Marquee",
            Lines = new[] { "DRIVE-IN", "CLO ED" },
            Face = new Color("b8b5a5"),
            Letters = new Color("453a2e"),
            Position = Prop.Anchor(Marquee.X, Marquee.Y),
        });

        foreach (Vector2I speaker in Speakers)
        {
            AddChild(new DriveInSpeaker
            {
                Position = Prop.Anchor(speaker.X, speaker.Y),
            });
        }

        foreach (int x in BenchX)
        {
            AddChild(new Prop
            {
                Name = $"Bench{x}",
                TexturePath = TownProps.TexturePath,
                Source = x % 2 == 0 ? TownProps.BenchB : TownProps.BenchA,
                Position = Prop.Anchor(x, BenchRow, 2),
            });
        }
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of the exit area (spawn-clearance rule).
        spawns.AddChild(SpawnMarker("from_road", 14, 3));
        spawns.AddChild(SpawnMarker("default", 15, 5));
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        // [KEVIN] placeholder copy: the marquee reads exactly what is drawn on it.
        AddChild(new Sign
        {
            Name = "MarqueeRead",
            DrawPlaceholder = false,
            Position = new Vector2(Marquee.X * TileSize + 8, Marquee.Y * TileSize + 8),
            Message = "The letter board spells CLO ED. It has for years.",
        });
    }

    private void BuildTravel()
    {
        AddRoadExit("NorthExit", MapIds.EastFork, "from_drive_in", DriveLeft, 0, widthTiles: 2);
    }

    // ------------------------------------------------------------------
    // Field dressing: ramp wear, cracks, and the weeds that win in the end.
    // Flat decal child of the Ground layer, same contract as the motel lot's.
    // ------------------------------------------------------------------

    private static readonly Color RampLine = new("b8b5a5");
    private static readonly Color Crack = new("3e4241");
    private static readonly Color WeedDark = new("2f5228");
    private static readonly Color Weed = new("457539");

    private Sprite2D BuildFieldMarkings()
    {
        int w = (LotRight - LotLeft + 1) * TileSize;   // 352
        int h = (LotBottom - LotTop + 1) * TileSize;   // 192
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Three ramp rows, the paint nearly gone: broken dashes, not lines.
        foreach (int y in new[] { 40, 104, 168 })
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
            Position = new Vector2(LotLeft * TileSize, LotTop * TileSize),
            Texture = ImageTexture.CreateFromImage(img),
        };
    }
}
