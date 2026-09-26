using Godot;
using TheHaunt.Content;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The west entry, 48x30 tiles: the frame a stranger sees first. The east-west road
/// on rows 14-15, the motor court long and low against the treeline on the north
/// side — office, four guest rooms, concrete walkway, asphalt lot and the googie
/// pole sign, all to the motel handoff's spec — with the gas station across the road
/// and the fireworks stand further along (src/Content/Places). The west mouth is
/// the road out of town, and for a resident it only leads back in: walking off the
/// west edge wraps to the east entry's east mouth (<see cref="RoadWrap"/>).
///
/// The handoff frames the motel as its own 26x18 map (an assumption it flags), but
/// the story doc places it IN the west entry scene, so its 16-tile-row layout
/// transposes here: facade rows 1-7, walkway row 8, lot rows 9-13 (the handoff's
/// six lot rows compressed to five), the existing road, one tile of x-offset.
/// Handoff tile (x, y) = this map's (x + 1, y - 1).
///
/// The gas station, the stand and the repair garage still ship as
/// <see cref="PlaceholderBuilding"/>s, now wearing their sign mounts. The garage
/// (src/Content/Places) starts shut and FOR SALE — its board opens WorldSim's
/// garage-sale session — and its door is deed-locked (garage.deed): buying the
/// place opens the shop floor behind it (<see cref="GarageInteriorMap"/>, Kevin's
/// 2026-08-30 garage-operation commission). The dark band sign stays dark either
/// way; the hardware store keeps the fully-shut treatment alone now.
/// </summary>
public partial class WestEntryMap : ExteriorMap
{
    private const int Width = 48;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

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

    // The motel's placement cell: the soda-machine column west of the office, on the
    // door row. Every part of the court is an offset from it.
    private static readonly Vector2I MotelCell = new(OfficeLeft - 1, DoorRow);

    /// <summary>The motor court's footprint — office and room strip as blocked
    /// rectangles (face rows through the door row), the office door column and the
    /// four room door columns on the door row — derived from the motel's placed cell.
    /// Read-only, for the world dump and the lot; null before the build, or when no
    /// motel is placed.</summary>
    internal (Rect2I Office, Rect2I Strip, int DoorRow, int OfficeDoorX, IReadOnlyList<int> RoomDoorX)? MotorCourt
    {
        get;
        private set;
    }

    // Footprints south of the road: (left, top, right, bottom); faces drawn 2 rows taller.
    private const int GasLeft = 24, GasTop = 18, GasRight = 29, GasBottom = 20;
    private const int GarageLeft = 32, GarageTop = 18, GarageRight = 37, GarageBottom = 20;
    private const int StandLeft = 33, StandTop = 10, StandRight = 35, StandBottom = 11;
    private const int GasDoorX = 26;
    // Where the garage's drawn door lands (centre of the face) — the kerb cut, the
    // FOR SALE board, and the deed-locked Door node all key off it.
    private const int GarageDoorX = 34;

    // The two cobra heads stand in the lot's kerb verge, flanking the driveway.
    // The east one is DEAD and stays dead — not flickering, dead — which keeps the
    // vacancy sign's V the only animated thing in the game (motel handoff).
    private static readonly Vector2I WestLight = new(13, 13);
    private static readonly Vector2I EastLight = new(22, 13);
    private static readonly Vector2I StandSign = new(31, 12);
    private static readonly Vector2I SaleSign = new(36, 21);

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.WestEntry;
        base._EnterTree();
    }

    // One car per occupied room, keyed by room, diffed by ApplyState like every
    // other model-derived view. Muted flats from the pre-art town's range; Pell's
    // slate sedan sits under room 3's lit window.
    private static readonly Color[] CarPaints =
    {
        new("6d6a58"), new("7a5a4c"), new("5c6a76"), new("6e5e68"),
    };
    private readonly Dictionary<int, GuestCar> _cars = new();

    /// <summary>
    /// The west entry's model-derived staging: the guest cars. Occupancy is story
    /// state (<see cref="MotelRules.OccupiedRooms"/>), and this runs on hydrate,
    /// every flag change and every dawn — so a guest checking in or out someday
    /// changes the lot with no new plumbing.
    /// </summary>
    public override void ApplyState(MapState state)
    {
        // No court or no lot placed: nowhere to park.
        if (MotorCourt == null || LotStalls == null)
            return;
        IReadOnlyList<int> occupied = MotelRules.OccupiedRooms(SaveService.Instance.Current);

        List<int>? departed = null;
        foreach (int room in _cars.Keys)
        {
            if (!occupied.Contains(room))
                (departed ??= new()).Add(room);
        }
        if (departed != null)
        {
            foreach (int room in departed)
            {
                _cars[room].QueueFree();
                _cars.Remove(room);
            }
        }

        foreach (int room in occupied)
        {
            if (_cars.ContainsKey(room))
                continue;
            // Nose-in to the motel in the stall under the guest's own door (Kevin
            // 2026-09-24), centred between its two stripes both ways.
            Rect2I stall = RoomStallPx(room);
            var car = new GuestCar
            {
                Name = $"GuestCar{room}",
                Paint = CarPaints[(room - 1) % CarPaints.Length],
                NoseIn = true,
                Position = new Vector2(stall.Position.X + stall.Size.X / 2f,
                    stall.End.Y - (stall.Size.Y - GuestCar.RearDepth) / 2f),
            };
            _cars[room] = car;
            AddChild(car);
        }
    }

    protected override void BuildDefaultSurfaces()
    {
        ResetSurfaces();

        // The road, open at both mouths: west out of town, east toward Billie's.
        for (int x = 0; x < Width; x++)
        {
            Set(x, RoadTop, Surface.Road);
            Set(x, RoadBottom, Surface.Road);
        }

        // The court's poured ground. The walkway runs the office frontage too, and
        // the kerb draws itself wherever the lot sits directly below (ExteriorMap).
        Fill(WalkLeft, WalkRow, WalkRight, WalkRow, Surface.Concrete);
        Fill(LotLeft, LotTop, LotRight, LotBottom, Surface.Asphalt);

        // Gravel under the placeholder buildings and one apron row below, so no face
        // draws a grass edge against its own frontage.
        Fill(GasLeft, GasTop, GasRight, GasBottom + 1, Surface.Gravel);
        Fill(GarageLeft, GarageTop, GarageRight, GarageBottom + 1, Surface.Gravel);
        Fill(StandLeft, StandTop, StandRight, StandBottom + 1, Surface.Gravel);
    }

    /// <summary>The west entry's placements as the C# literals describe them: the seed
    /// <c>data/maps/west_entry.tmx</c> was written from, and the fallback when it is missing.</summary>
    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.WestEntry);

        // The motor court, its pole sign in the grass between the office and the road,
        // the buildings south of the road and the stand, then the two cobra heads.
        recipe.Add(PlacementKinds.Prop, "motel", MotelCell.X, MotelCell.Y);
        recipe.Add(PlacementKinds.Prop, "motel_sign", SignFoot.X, SignFoot.Y);
        recipe.Add(PlacementKinds.Prop, "gas_station", GasLeft, GasBottom);
        recipe.Add(PlacementKinds.Prop, "garage", GarageLeft, GarageBottom);
        recipe.Add(PlacementKinds.Prop, "fireworks_stand", StandLeft, StandBottom);
        recipe.Add(PlacementKinds.Prop, "fireworks_pole", StandSign.X, StandSign.Y);
        recipe.Add(PlacementKinds.Prop, "west_light", WestLight.X, WestLight.Y);
        recipe.Add(PlacementKinds.Prop, "east_light_dead", EastLight.X, EastLight.Y);
        // The garage's FOR SALE board — south of the footprint for the same Y-sort
        // reason as the gas sign, east of the drawn doorway. A press opens the sale
        // session; once the deed lands the same node answers SOLD.
        recipe.Add(PlacementKinds.Prop, "garage_sale_sign", SaleSign.X, SaleSign.Y);

        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule). The
        // wrap marker is where a resident who left east finds themselves arriving.
        recipe.Add(PlacementKinds.Spawn, "default", 24, 15);
        recipe.Add(PlacementKinds.Spawn, RoadWrap.ArrivalSpawn, 2, 15);
        recipe.Add(PlacementKinds.Spawn, "from_billies", 45, 15);
        recipe.Add(PlacementKinds.Spawn, "from_motel", OfficeDoorX, WalkRow);
        for (int room = 1; room <= MotelRules.Rooms; room++)
            recipe.Add(PlacementKinds.Spawn, $"from_room{room}", RoomDoorX[room - 1], WalkRow);
        recipe.Add(PlacementKinds.Spawn, "from_gas", GasDoorX, GasBottom + 1);
        recipe.Add(PlacementKinds.Spawn, "from_garage", GarageDoorX, GarageBottom + 1);

        // The pole sign's read area rides its foot tile; the art draws it, the node
        // answers for it. The gas board stands south of the footprint (a sign north of
        // a south-of-road building lands inside its drawn face and Y-sorts invisible),
        // east of the doorway so the door approach stays clear. Copy lives with the
        // places (src/Content/Places).
        recipe.Add(PlacementKinds.Sign, "MotelSignRead", SignFoot.X, SignFoot.Y)
            .SetBool(PlacementFields.Board, false);
        recipe.Add(PlacementKinds.Sign, "GasSign", 28, 21);
        recipe.Add(PlacementKinds.Sign, "FireworksSign", 36, 12);

        // Every doorway is drawn into its face. The office is the only door open at
        // first contact; rooms 1-4 are locked behind their own flags and the garage
        // behind its deed (ConfigureDoor), and a locked handle answers with a line —
        // never silence (motel handoff).
        recipe.Add(PlacementKinds.Door, MapIds.Motel, OfficeDoorX, DoorRow)
            .SetText(PlacementFields.Spawn, "entry");
        for (int room = 1; room <= MotelRules.Rooms; room++)
        {
            recipe.Add(PlacementKinds.Door, MapIds.MotelRoom(room), RoomDoorX[room - 1], DoorRow)
                .SetText(PlacementFields.Spawn, "entry");
        }
        recipe.Add(PlacementKinds.Door, MapIds.GasStation, GasDoorX, GasBottom)
            .SetText(PlacementFields.Spawn, "entry");
        recipe.Add(PlacementKinds.Door, MapIds.GarageInterior, GarageDoorX, GarageBottom)
            .SetText(PlacementFields.Spawn, "entry");

        // The road out. It goes exactly where the story says it goes.
        AddExit(recipe, RoadWrap.PastTheWestEdgeMap, RoadWrap.ArrivalSpawn, 0, RoadTop, 1, 2);
        AddExit(recipe, MapIds.Billies, "from_west_entry", Width - 1, RoadTop, 1, 2);

        // Kerb cuts: the lot driveway (a cut, not an apron), the gas frontage, and the
        // garage frontage — a repair shop lived off cars rolling in.
        AddKerbCut(recipe, "lot_driveway", LotLeft + 2, RoadTop, 4);
        AddKerbCut(recipe, "gas_frontage", GasDoorX, RoadBottom, 2);
        AddKerbCut(recipe, "garage_frontage", GarageDoorX, RoadBottom, 2);

        return recipe;
    }

    private static void AddExit(MapRecipe recipe, string target, string spawn, int x, int y, int w, int h)
    {
        MapPlacement exit = recipe.Add(PlacementKinds.Exit, target, x, y);
        exit.SetText(PlacementFields.Spawn, spawn);
        exit.SetInt(PlacementFields.Width, w);
        exit.SetInt(PlacementFields.Height, h);
    }

    private static void AddKerbCut(MapRecipe recipe, string id, int x, int y, int w)
    {
        MapPlacement cut = recipe.Add(PlacementKinds.KerbCut, id, x, y);
        cut.SetInt(PlacementFields.Width, w);
        cut.SetInt(PlacementFields.Height, 1);
    }

    protected override string? SignTextFor(string signId) =>
        MotelRules.SignTextFor(signId) ?? GasStation.SignTextFor(signId) ?? FireworksStand.SignTextFor(signId);

    protected override IReadOnlyDictionary<string, ExteriorProp> PropCatalog() =>
        new Dictionary<string, ExteriorProp>(StringComparer.Ordinal)
        {
            // The full drawn face blocks — the court backs onto the treeline and nothing
            // passes behind it. The door cells stay open; each Door carries its own
            // blocker. The drawn face bleeds past its blocked cells at both ends — the
            // soda machine on the office's west column, the strip outline's sliver past
            // its east end — and nothing passes behind this building, so both edge
            // columns block too.
            ["motel"] = new(BuildMotel,
                new Rect2I(0, FaceTop - DoorRow, OfficeRight - MotelCell.X + 1, DoorRow - FaceTop + 1),
                new Rect2I(StripLeft - MotelCell.X, FaceTop - DoorRow, StripRight - StripLeft + 2, DoorRow - FaceTop + 1)),
            // The pole sign, in the grass between the office and the road; its read
            // area (MotelSignRead) carries the blocker.
            ["motel_sign"] = new(p => new MotelSign
            {
                Name = "MotelSign",
                Position = Prop.Anchor(p.X, p.Y),
            }),
            ["gas_station"] = new(BuildGasStation, ExteriorProp.Rows(GasRight - GasLeft + 1, GasBottom - GasTop + 1)),
            // The garage's door cell gaps like the gas station's: the Door node's own
            // blocker seals it, and the handle is deed-locked (garage.deed).
            ["garage"] = new(BuildGarage, ExteriorProp.Rows(GarageRight - GarageLeft + 1, GarageBottom - GarageTop + 1)),
            ["fireworks_stand"] = new(p => new PlaceholderBuilding
            {
                Name = "FireworksStand",
                TilesWide = StandRight - StandLeft + 1,
                FootprintRows = StandBottom - StandTop + 1,
                Wall = new Color("8a6a45"),
                Position = Prop.Anchor(p.X, p.Y, StandRight - StandLeft + 1),
            }, ExteriorProp.Rows(StandRight - StandLeft + 1, StandBottom - StandTop + 1)),
            // A stand that lives off passing traffic gets the pole mount.
            ["fireworks_pole"] = new(p => new PoleSign
            {
                Name = "FireworksPole",
                Lines = new[] { "FIREWORKS" },
                Position = Prop.Anchor(p.X, p.Y),
            }, ExteriorProp.Rows(1, 1)),
            // The two cobra heads stand in the lot's kerb verge, flanking the driveway.
            // The east one is DEAD and stays dead — not flickering, dead — which keeps
            // the vacancy sign's V the only animated thing in the game (motel handoff).
            ["west_light"] = new(p => new StreetLight
            {
                Name = "WestLight",
                Position = Prop.Anchor(p.X, p.Y),
            }, ExteriorProp.Rows(1, 1)),
            ["east_light_dead"] = new(p => new StreetLight
            {
                Name = "EastLightDead",
                Lit = false,
                ArmLeft = true,
                Position = Prop.Anchor(p.X, p.Y),
            }, ExteriorProp.Rows(1, 1)),
            ["garage_sale_sign"] = new(p => new GarageSaleSign
            {
                Name = "GarageSaleSign",
                Position = CellCentre(p),
            }),
        };

    private Node2D BuildMotel(MapPlacement p)
    {
        Vector2I m = p.Cell;
        MotorCourt = (
            new Rect2I(m.X + (OfficeLeft - MotelCell.X), m.Y + (FaceTop - DoorRow),
                OfficeRight - OfficeLeft + 1, DoorRow - FaceTop + 1),
            new Rect2I(m.X + (StripLeft - MotelCell.X), m.Y + (FaceTop - DoorRow),
                StripRight - StripLeft + 1, DoorRow - FaceTop + 1),
            m.Y, m.X + (OfficeDoorX - MotelCell.X),
            RoomDoorX.Select(x => m.X + (x - MotelCell.X)).ToArray());

        // The face spans map px 18-418; anchored bottom-centre on the door row's
        // south edge, like every facade.
        // +2: the face's last two texture rows are the handoff's ink base band
        // below the kick plate, drawn over the walkway's top edge like ground contact.
        return new MotelFacade
        {
            Name = "MotelFacade",
            Position = new Vector2(m.X * TileSize + 202, (m.Y + 1) * TileSize + 2),
        };
    }

    private static Node2D BuildGasStation(MapPlacement p)
    {
        var gas = new PlaceholderBuilding
        {
            Name = "GasStation",
            TilesWide = GasRight - GasLeft + 1,
            FootprintRows = GasBottom - GasTop + 1,
            Wall = new Color("8a7a6a"),
            Position = Prop.Anchor(p.X, p.Y, GasRight - GasLeft + 1),
        };
        // Window mount (motel handoff §3): band over the glass, neon word inside it —
        // lit exactly while the counter is staffed, so the sign never lies about Dennis.
        gas.AddChild(new WallBandSign { Text = "GAS", Position = new Vector2(0, -57) });
        gas.AddChild(new NeonWordSign
        {
            Word = "OPEN",
            OnAt = m => m is >= GasStation.OpenMinute and < GasStation.CloseMinute,
            Position = new Vector2(-24, -39),
        });
        return gas;
    }

    private static Node2D BuildGarage(MapPlacement p)
    {
        // The repair garage, shut and for sale (src/Content/Places): Jane's route
        // back into her father's trade once the skills system lands. Same closed-
        // and-dark treatment as the hardware store — the band sign's bill is not
        // being paid, and the missing OPEN neon is itself the tell.
        var garage = new PlaceholderBuilding
        {
            Name = "Garage",
            TilesWide = GarageRight - GarageLeft + 1,
            FootprintRows = GarageBottom - GarageTop + 1,
            Wall = new Color("6d7a72"),
            Position = Prop.Anchor(p.X, p.Y, GarageRight - GarageLeft + 1),
        };
        garage.AddChild(new WallBandSign
        {
            Text = "GARAGE",
            LitAtNight = false,
            Position = new Vector2(0, -57),
        });
        return garage;
    }

    protected override void ConfigureDoor(Door door)
    {
        // Locked-handle lines live with the motel (src/Content/Places/MotelRules.cs).
        // Room 3 is Pell's — the radio never explains itself.
        for (int room = 1; room <= MotelRules.Rooms; room++)
        {
            if (door.TargetMapId != MapIds.MotelRoom(room))
                continue;
            door.RequiredFlag = MotelRules.RoomFlag(room);
            door.LockedMessage = room == 3 ? MotelRules.Room3LockedLine : MotelRules.RoomLockedLine;
        }

        // The garage's shut door, deed-locked (the motel-room pattern: live flag
        // check per interact, no repaint on purchase). Before the deed it answers
        // with a line; after it, Jane walks into her shop at any hour — the 9-6
        // window gates customers and Mike, never the owner.
        if (door.TargetMapId == MapIds.GarageInterior)
        {
            door.RequiredFlag = StoryKeys.GarageDeed;
            door.LockedMessage = Garage.DoorLockedLine;   // copy with the place (Places/Garage.cs)
        }
    }

    protected override void BuildDressing(TileMapLayer ground)
    {
        if (LotStalls is { } stalls)
            ground.AddChild(BuildLotMarkings(stalls.Lot, stalls.StripesPx));
    }

    // ------------------------------------------------------------------
    // Lot dressing — flat ground markings, drawn as a decal child of the Ground
    // layer so it renders over the tiles and under everything Y-sorted. Dressing,
    // not terrain: the act pass regenerates it the way it regenerates the tiles.
    // ------------------------------------------------------------------

    private static readonly Color StallStripe = new("b8b5a5");
    private static readonly Color Crack = new("3e4241");

    /// <summary>The asphalt lot (tiles — the bounding box of the map's Asphalt) and its
    /// eight faded stalls: nine stripes, 2x40, every 36px, 8px in from the lot's west
    /// edge (handoff geometry, one row shallower), as lot-local pixel rects. Read-only,
    /// for the world dump and the lot; null before the build, or when no Asphalt is painted.</summary>
    internal (Rect2I Lot, IReadOnlyList<Rect2I> StripesPx)? LotStalls
    {
        get
        {
            if (BoundsOf(Surface.Asphalt) is not { } lot)
                return null;
            var stripes = new List<Rect2I>();
            for (int x = 8; x + 2 <= lot.Size.X * TileSize; x += 36)
                stripes.Add(new Rect2I(x, 10, 2, 40));
            return (lot, stripes);
        }
    }

    /// <summary>The stall in front of a room (map px): the paint-free asphalt between
    /// the two stripes that hold the room's door column centre, as deep as the stripes
    /// run. Where that room's guest parks.</summary>
    internal Rect2I RoomStallPx(int room)
    {
        if (MotorCourt is not { } court || LotStalls is not { } stalls)
            throw new System.InvalidOperationException($"room {room}'s door has no stall in front of it");
        (Rect2I lot, IReadOnlyList<Rect2I> stripes) = stalls;
        Vector2I origin = lot.Position * TileSize;
        int door = court.RoomDoorX[room - 1] * TileSize + TileSize / 2;
        for (int i = 0; i + 1 < stripes.Count; i++)
        {
            Rect2I west = stripes[i], east = stripes[i + 1];
            int x0 = origin.X + west.End.X, x1 = origin.X + east.Position.X;
            if (x0 <= door && door < x1)
                return new Rect2I(x0, origin.Y + west.Position.Y, x1 - x0, west.Size.Y);
        }
        throw new System.InvalidOperationException($"room {room}'s door has no stall in front of it");
    }

    private static Sprite2D BuildLotMarkings(Rect2I lot, IReadOnlyList<Rect2I> stripes)
    {
        int w = lot.Size.X * TileSize;   // 320
        int h = lot.Size.Y * TileSize;   // 80
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        foreach (Rect2I stripe in stripes)
            img.FillRect(stripe, StallStripe);

        // Scattered short cracks across the south half.
        for (int i = 0; i < 40; i++)
        {
            int cx = (Hash(i, 3) % (w - 8));
            int cy = 46 + Hash(i, 7) % (h - 52);
            int len = 1 + Hash(i, 11) % 7;
            img.FillRect(new Rect2I(cx, cy, len, 1), Crack);
        }

        return new Sprite2D
        {
            Name = "LotMarkings",
            Centered = false,
            Position = new Vector2(lot.Position.X * TileSize, lot.Position.Y * TileSize),
            Texture = ImageTexture.CreateFromImage(img),
        };
    }
}
