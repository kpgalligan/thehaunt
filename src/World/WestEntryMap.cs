using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The west entry, 48x30 tiles: the frame a stranger sees first. The east-west road
/// on rows 14-15, the motor court long and low against the treeline on the north
/// side — office, four guest rooms, concrete walkway, asphalt lot and the googie
/// pole sign, all to the motel handoff's spec — with the gas station across the road
/// and the fireworks stand further along (docs/story/README.md). The west mouth is
/// the road out of town, and for a resident it only leads back in: walking off the
/// west edge wraps to the east entry's east mouth (<see cref="RoadWrap"/>).
///
/// The handoff frames the motel as its own 26x18 map (an assumption it flags), but
/// the story doc places it IN the west entry scene, so its 16-tile-row layout
/// transposes here: facade rows 1-7, walkway row 8, lot rows 9-13 (the handoff's
/// six lot rows compressed to five), the existing road, one tile of x-offset.
/// Handoff tile (x, y) = this map's (x + 1, y - 1).
///
/// The gas station and the stand still ship as <see cref="PlaceholderBuilding"/>s,
/// now wearing their sign mounts.
/// </summary>
public partial class WestEntryMap : ExteriorMap
{
    private const int Width = 48;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    private const int RoadTop = 14, RoadBottom = 15;

    // The motor court: office wall x2-6, a grass gap at x7, room strip x8-25, all on
    // face rows 1-7 with the doors on row 7. Walkway row 8, lot rows 9-13.
    private const int FaceTop = 1, DoorRow = 7;
    private const int OfficeLeft = 2, OfficeRight = 6;
    private const int StripLeft = 8, StripRight = 25;
    private const int WalkRow = 8, WalkLeft = 2, WalkRight = 27;
    private const int LotTop = 9, LotBottom = 13, LotLeft = 7, LotRight = 26;
    private const int OfficeDoorX = 6;
    private static readonly int[] RoomDoorX = { 8, 13, 17, 21 };
    private static readonly Vector2I SignFoot = new(4, 13);

    // Footprints south of the road: (left, top, right, bottom); faces drawn 2 rows taller.
    private const int GasLeft = 24, GasTop = 18, GasRight = 29, GasBottom = 20;
    private const int StandLeft = 33, StandTop = 10, StandRight = 35, StandBottom = 11;
    private const int GasDoorX = 26;

    private static readonly Vector2I Lamp = new(30, 12);
    private static readonly Vector2I StandSign = new(31, 12);

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.WestEntry;
        base._EnterTree();
    }

    public override void _Ready()
    {
        BuildSurfaces();
        TileSet tileSet = RoadsideTerrain.Get(); // the lot needs the asphalt source
        TileMapLayer ground = BuildGround(tileSet);
        ground.AddChild(BuildLotMarkings());
        BuildObstacles(tileSet);
        BuildBuildings();
        BuildSpawns();
        BuildInteractables();
        BuildTravel();
    }

    private void BuildSurfaces()
    {
        ResetSurfaces();

        // The road, open at both mouths: west out of town, east toward Billie's.
        for (int x = 0; x < Width; x++)
        {
            Set(x, RoadTop, Surface.Dirt);
            Set(x, RoadBottom, Surface.Dirt);
        }

        // The court's poured ground. The walkway runs the office frontage too, and
        // the kerb draws itself wherever the lot sits directly below (ExteriorMap).
        Fill(WalkLeft, WalkRow, WalkRight, WalkRow, Surface.Concrete);
        Fill(LotLeft, LotTop, LotRight, LotBottom, Surface.Asphalt);

        // Gravel under the placeholder buildings and one apron row below, so no face
        // draws a grass edge against its own frontage.
        Fill(GasLeft, GasTop, GasRight, GasBottom + 1, Surface.Gravel);
        Fill(StandLeft, StandTop, StandRight, StandBottom + 1, Surface.Gravel);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };

        // The full drawn face blocks — the court backs onto the treeline and nothing
        // passes behind it. Door cells stay open; each Door carries its own blocker.
        Block(obstacles, OfficeLeft, FaceTop, OfficeRight, DoorRow, OfficeDoorX, DoorRow);
        Block(obstacles, StripLeft, FaceTop, StripRight, DoorRow - 1);
        for (int x = StripLeft; x <= StripRight; x++)
        {
            if (System.Array.IndexOf(RoomDoorX, x) < 0)
                obstacles.SetCell(new Vector2I(x, DoorRow), 0, TerrainTiles.Blocker);
        }

        // The drawn face bleeds past its blocked cells at both ends — the soda
        // machine on column x1, the strip outline's sliver on x26 — and nothing
        // passes behind this building, so both edge columns block too.
        Block(obstacles, OfficeLeft - 1, FaceTop, OfficeLeft - 1, DoorRow);
        Block(obstacles, StripRight + 1, FaceTop, StripRight + 1, DoorRow);

        Block(obstacles, GasLeft, GasTop, GasRight, GasBottom, GasDoorX, GasBottom);
        Block(obstacles, StandLeft, StandTop, StandRight, StandBottom);
        obstacles.SetCell(Lamp, 0, TerrainTiles.Blocker);
        obstacles.SetCell(StandSign, 0, TerrainTiles.Blocker);
        AddChild(obstacles);
    }

    private void BuildBuildings()
    {
        // The face spans map px 18-418; anchored bottom-centre on the door row's
        // south edge, like every facade.
        // +2: the face's last two texture rows are the handoff's ink base band
        // below the kick plate, drawn over the walkway's top edge like ground contact.
        AddChild(new MotelFacade
        {
            Name = "MotelFacade",
            Position = new Vector2(218, (DoorRow + 1) * TileSize + 2),
        });

        // The pole sign, in the grass between the office and the road.
        AddChild(new MotelSign
        {
            Name = "MotelSign",
            Position = Prop.Anchor(SignFoot.X, SignFoot.Y),
        });

        var gas = new PlaceholderBuilding
        {
            Name = "GasStation",
            TilesWide = GasRight - GasLeft + 1,
            FootprintRows = GasBottom - GasTop + 1,
            Wall = new Color("8a7a6a"),
            Position = Prop.Anchor(GasLeft, GasBottom, GasRight - GasLeft + 1),
        };
        // Window mount (motel handoff §3): band over the glass, neon word inside it —
        // lit exactly while the counter is staffed, so the sign never lies about Dennis.
        gas.AddChild(new WallBandSign { Text = "GAS", Position = new Vector2(0, -57) });
        gas.AddChild(new NeonWordSign
        {
            Word = "OPEN",
            OnAt = m => m is >= NpcSchedules.GasOpenMinute and < NpcSchedules.GasCloseMinute,
            Position = new Vector2(-24, -39),
        });
        AddChild(gas);

        AddChild(new PlaceholderBuilding
        {
            Name = "FireworksStand",
            TilesWide = StandRight - StandLeft + 1,
            FootprintRows = StandBottom - StandTop + 1,
            Wall = new Color("8a6a45"),
            Position = Prop.Anchor(StandLeft, StandBottom, StandRight - StandLeft + 1),
        });
        // A stand that lives off passing traffic gets the pole mount.
        AddChild(new PoleSign
        {
            Name = "FireworksPole",
            Lines = new[] { "FIREWORKS" },
            Position = Prop.Anchor(StandSign.X, StandSign.Y),
        });

        AddChild(new LampPost { Position = Prop.Anchor(Lamp.X, Lamp.Y) });
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule). The
        // wrap marker is where a resident who left east finds themselves arriving.
        spawns.AddChild(SpawnMarker("default", 24, 15));
        spawns.AddChild(SpawnMarker(RoadWrap.ArrivalSpawn, 2, 15));
        spawns.AddChild(SpawnMarker("from_billies", 45, 15));
        spawns.AddChild(SpawnMarker("from_motel", OfficeDoorX, WalkRow));
        for (int room = 1; room <= MotelRules.Rooms; room++)
            spawns.AddChild(SpawnMarker($"from_room{room}", RoomDoorX[room - 1], WalkRow));
        spawns.AddChild(SpawnMarker("from_gas", GasDoorX, GasBottom + 1));
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        // The pole sign's read area rides its foot tile; the art draws it, the node
        // answers for it. [KEVIN] placeholder copy — restates only what is drawn.
        AddChild(new Sign
        {
            Name = "MotelSignRead",
            DrawPlaceholder = false,
            Position = new Vector2(SignFoot.X * TileSize + 8, SignFoot.Y * TileSize + 8),
            Message = "MOTEL. Under it, a blank nameplate. NO VACANCY — the NO is dark.",
        });
        // [KEVIN] placeholder copy on both — canon restatement only, no names.
        AddChild(new Sign
        {
            Name = "GasSign",
            // South of the footprint: a sign north of a south-of-road building lands
            // inside its drawn face and Y-sorts invisible. East of the doorway so the
            // door approach stays clear.
            Position = new Vector2(28 * TileSize + 8, 21 * TileSize + 8),
            Message = "Gas.",
        });
        AddChild(new Sign
        {
            Name = "FireworksSign",
            Position = new Vector2(36 * TileSize + 8, 12 * TileSize + 8),
            Message = "Fireworks.",
        });
    }

    private void BuildTravel()
    {
        // The road out. It goes exactly where the story says it goes.
        AddRoadExit("WestExit", RoadWrap.PastTheWestEdgeMap, RoadWrap.ArrivalSpawn, 0, RoadTop);
        AddRoadExit("EastExit", MapIds.Billies, "from_west_entry", Width - 1, RoadTop);

        // Every doorway is drawn into its face, so the Door nodes contribute their
        // blockers and their prompts only. The office is the only door open at first
        // contact; rooms 1-4 are locked behind their own flags, and a locked handle
        // answers with a line — never silence (motel handoff).
        AddChild(new Door
        {
            Name = "MotelDoor",
            TargetMapId = MapIds.Motel,
            TargetSpawnId = "entry",
            DrawPlaceholder = false,
            Position = new Vector2(OfficeDoorX * TileSize + 8, DoorRow * TileSize + 8),
        });
        for (int room = 1; room <= MotelRules.Rooms; room++)
        {
            AddChild(new Door
            {
                Name = $"Room{room}Door",
                TargetMapId = MapIds.MotelRoom(room),
                TargetSpawnId = "entry",
                RequiredFlag = MotelRules.RoomFlag(room),
                // [KEVIN] locked-handle lines. Room 3 is Pell's — the radio is the
                // court's one look-twice tell, and it never explains itself.
                LockedMessage = room == 3 ? "Locked. A radio plays low inside." : "Locked.",
                DrawPlaceholder = false,
                Position = new Vector2(RoomDoorX[room - 1] * TileSize + 8, DoorRow * TileSize + 8),
            });
        }
        AddChild(new Door
        {
            Name = "GasDoor",
            TargetMapId = MapIds.GasStation,
            TargetSpawnId = "entry",
            DrawPlaceholder = false,
            Position = new Vector2(GasDoorX * TileSize + 8, GasBottom * TileSize + 8),
        });
    }

    // ------------------------------------------------------------------
    // Lot dressing — flat ground markings, drawn as a decal child of the Ground
    // layer so it renders over the tiles and under everything Y-sorted. Dressing,
    // not terrain: the act pass regenerates it the way it regenerates the tiles.
    // ------------------------------------------------------------------

    private static readonly Color StallStripe = new("b8b5a5");
    private static readonly Color Crack = new("3e4241");
    private static readonly Color Wear = new("8a6a45");

    private Sprite2D BuildLotMarkings()
    {
        int w = (LotRight - LotLeft + 1) * TileSize;   // 320
        int h = (LotBottom - LotTop + 1) * TileSize;   // 80
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Eight faded stalls: nine stripes, 2x40, every 36px, 8px in from the lot's
        // west edge (handoff geometry, one row shallower).
        for (int x = 8; x + 2 <= w; x += 36)
            img.FillRect(new Rect2I(x, 10, 2, 40), StallStripe);

        // Scattered short cracks across the south half.
        for (int i = 0; i < 40; i++)
        {
            int cx = (Hash(i, 3) % (w - 8));
            int cy = 46 + Hash(i, 7) % (h - 52);
            int len = 1 + Hash(i, 11) % 7;
            img.FillRect(new Rect2I(cx, cy, len, 1), Crack);
        }

        // The entrance apron: dirt worn through where cars actually turn in.
        img.FillRect(new Rect2I(36, h - 6, 64, 6), Wear);

        return new Sprite2D
        {
            Name = "LotMarkings",
            Centered = false,
            Position = new Vector2(LotLeft * TileSize, LotTop * TileSize),
            Texture = ImageTexture.CreateFromImage(img),
        };
    }
}
