using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The repair garage's shop floor, 15x10 tiles (Kevin's 2026-08-30 garage
/// commission): two car lifts against the north wall with oil-stained bays, the
/// workbench and tool rack Jane's father would recognize along the west wall,
/// and Mike's little counter by the east — he takes the customers, he doesn't
/// touch the cars. Reached through the west entry's deed-locked door; Jane can
/// be in here at any hour (the 9-6 window gates arrivals and Mike, not her).
///
/// The cars are the map's model-derived staging (the guest-car pattern):
/// ApplyState diffs one GuestCar per occupied lift from GameData.GarageJobs —
/// on hydrate, every repaint, every work press and every dawn — with a floating
/// progress label per bay. Lift cells are Block()ed PERMANENTLY, car or no car,
/// so an hourly arrival can never spawn a blocker on top of the player.
/// </summary>
public partial class GarageInteriorMap : InteriorMap
{
    protected override int Width => 15;
    protected override int Height => 10;
    protected override InteriorTiles.WallSet Walls { get; } =
        new(InteriorTiles.WallStone, InteriorTiles.CorniceStone);
    protected override int DoorX => 6;
    protected override int DoorY => 9;

    protected override Vector2I[] Floor { get; } =
    {
        InteriorTiles.FloorStone[0], InteriorTiles.FloorStone[1],
    };

    private const int LiftRow = 2;
    private static readonly int[] LiftLeft = { 2, 7 };   // west cell of each 3-tile bay

    private const int CounterLeft = 11, CounterRight = 13, CounterRow = 4;

    // One muted flat per service, so the bay tells you the job at a glance.
    private static readonly Color FallbackPaint = new("6d6a58");
    private static Color PaintFor(string serviceId) => serviceId switch
    {
        GarageServices.OilChange => new Color("5c6a76"),      // slate
        GarageServices.Lights => new Color("7a5a4c"),         // rust
        GarageServices.Transmission => new Color("6e5e68"),   // plum-grey
        _ => FallbackPaint,
    };

    private readonly Node2D[] _bays = new Node2D[GarageOpsRules.MaxCars];
    private readonly GuestCar?[] _cars = new GuestCar?[GarageOpsRules.MaxCars];
    private readonly string?[] _carKeys = new string?[GarageOpsRules.MaxCars];
    private readonly Label[] _labels = new Label[GarageOpsRules.MaxCars];

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.GarageInterior;
        base._EnterTree();
    }

    protected override void Decorate()
    {
        // North wall: two dark windows over the bays, the licence plaque by the
        // counter. The bays themselves wear the floor's oil stains.
        SetWall(3, 0, InteriorTiles.WindowDark);
        SetWall(8, 0, InteriorTiles.WindowDark);
        SetWall(12, 0, InteriorTiles.Plaque);
        foreach (int left in LiftLeft)
        {
            for (int x = left; x <= left + 2; x++)
            {
                SetFloor(x, LiftRow, InteriorTiles.FloorStain);
                Block(x, LiftRow);   // permanent — see class doc
            }
        }

        // The trade's furniture along the west wall; stock that never got shelved.
        AddFurniture(Furniture.ToolRack, 1, 4);
        AddFurniture(Furniture.Workbench, 1, 6);
        SetWall(1, 8, InteriorTiles.Barrel);

        // Mike's counter, till toward the room; crates behind the customer side.
        AddCounter(CounterLeft, CounterRight, CounterRow);
        AddFurniture(Furniture.Till, CounterLeft + 1, CounterRow, blocks: false);
        AddFurniture(Furniture.Crates, 12, 7);
        AddFurniture(Furniture.Sack, 13, 6);
    }

    protected override void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 8 * TileSize + 8), // (104, 136)
        });
        spawns.AddChild(new Marker2D
        {
            Name = "default",
            Position = new Vector2(6 * TileSize + 8, 5 * TileSize + 8), // (104, 88)
        });
        AddChild(spawns);
    }

    protected override void BuildInteractables()
    {
        AddChild(new Door
        {
            Name = "OutDoor",
            TargetMapId = MapIds.WestEntry,
            TargetSpawnId = "from_garage",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8),
        });

        for (int lift = 0; lift < GarageOpsRules.MaxCars; lift++)
        {
            Vector2 anchor = Prop.Anchor(LiftLeft[lift], LiftRow, 3);

            // The bay container Y-sorts on the lift's base row; the CarLift draws
            // first so a diffed-in GuestCar wins the tie and sits on the rails.
            var bay = new Node2D { Name = $"Bay{lift}", Position = anchor };
            bay.AddChild(new CarLift());
            var label = new Label
            {
                Visible = false,
                Scale = new Vector2(0.5f, 0.5f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            bay.AddChild(label);
            _bays[lift] = bay;
            _labels[lift] = label;
            AddChild(bay);

            AddChild(new LiftStation
            {
                Name = $"Lift{lift}",
                Lift = lift,
                Position = new Vector2((LiftLeft[lift] + 1) * TileSize + 8, LiftRow * TileSize + 8),
            });
        }
    }

    /// <summary>The garage's model-derived staging: one car per occupied lift.</summary>
    public override void ApplyState(MapState state)
    {
        GameData data = SaveService.Instance.Current;
        for (int lift = 0; lift < GarageOpsRules.MaxCars; lift++)
        {
            GarageJobRecord? job = GarageOpsRules.JobAt(data, lift);

            // Identity = which customer's car this is; progress mutates in place.
            string? key = job == null ? null : $"{job.ArrivalDay}:{job.ArrivalHour}:{job.ServiceId}";
            if (key != _carKeys[lift])
            {
                if (_cars[lift] is { } departing)
                {
                    departing.QueueFree();
                    _cars[lift] = null;
                }
                if (job != null)
                {
                    var car = new GuestCar { Paint = PaintFor(job.ServiceId) };
                    _cars[lift] = car;
                    _bays[lift].AddChild(car);
                }
                _carKeys[lift] = key;
            }

            Label label = _labels[lift];
            if (job == null)
            {
                label.Visible = false;
                continue;
            }
            int work = GarageServices.TryGet(job.ServiceId)?.Work ?? 0;
            label.Text = job.Completed || work <= 0
                ? "Done"
                : $"{job.WorkDone * 100 / work}%";
            label.ResetSize();
            Vector2 scaled = label.Size * label.Scale;
            label.Position = new Vector2(-scaled.X / 2f, -30f - scaled.Y / 2f);
            label.Visible = true;
        }
    }
}
