using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The farmhouse interior, 14x10 tiles, dressed from the farm/interiors handoff's
/// reference room. Log walls, plank floor, a hearth on the north side with the bed
/// beside it, and the stove and cupboard along the west end of the same wall.
///
/// Every gameplay position is exactly where it was: bed (12,2)-(12,3), chest (2,2),
/// table (6,4)+(7,4), door (7,9). The chest is drawn as the cupboard the reference room
/// puts on its cell, and the bed is the sheet's bed — both keep their own collision and
/// their own interaction, they just stopped being coloured rectangles.
/// </summary>
public partial class FarmHouseMap : InteriorMap
{
    protected override int Width => 14;
    protected override int Height => 10;
    protected override InteriorTiles.WallSet Walls => InteriorTiles.FarmhouseWalls;
    protected override int DoorX => 7;
    protected override int DoorY => 9;

    // The reference's three-step diagonal stagger — plank a, plank b, then a worn board.
    protected override Vector2I[] Floor { get; } =
    {
        InteriorTiles.FloorPlank[0], InteriorTiles.FloorPlank[1], InteriorTiles.FloorPlankWorn,
    };

    private const int HearthX = 10;   // hearth_l/c/r on (10..12, 1), fire at (11, 2)
    private const int BedX = 12;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.FarmHouse;
        base._EnterTree();
    }

    protected override void Decorate()
    {
        // Lit windows in the cornice row. They are drawn on a cream plaster ground, so
        // against the log wall they read as whitewashed trim around the glass — which is
        // how the reference room draws them too.
        SetWall(3, 0, InteriorTiles.WindowLit);
        SetWall(10, 0, InteriorTiles.WindowLit);

        AddHearth(HearthX, 1);

        // The rug is floor, not furniture: red on the left column, plum on the right.
        for (int y = 6; y <= 7; y++)
        {
            SetFloor(5, y, InteriorTiles.RugA);
            SetFloor(6, y, InteriorTiles.RugB);
        }

        AddFurniture(Furniture.Stove, 3, 2);
        AddFurniture(Furniture.Pot, 4, 2);
        AddFurniture(Furniture.ChairSide, 5, 4);
        AddFurniture(Furniture.Table, 6, 4);
        AddFurniture(Furniture.ChairBack, 8, 4);
        AddFurniture(Furniture.Lamp, 9, 6);
        AddFurniture(Furniture.Bucket, 1, 7);
        AddFurniture(Furniture.Sack, 12, 7);
    }

    protected override void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 8 * TileSize + 8), // (120, 136)
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
        var interactables = new Node2D { Name = "Interactables" };
        // The bed that used to sit outdoors on the farm — same class, new home, and now
        // the 16x32 piece from the sheet standing exactly on its two cells.
        interactables.AddChild(new Bed
        {
            Name = "Bed",
            ArtSource = Furniture.Bed,
            Position = new Vector2(BedX * TileSize + 8, 3 * TileSize), // centre of (12,2)-(12,3)
        });
        // The reference room draws the storage on its cell as a cupboard; the node is
        // unchanged, so the chest's contents still live in GameData.Storages.
        interactables.AddChild(new Chest
        {
            Name = "Chest",
            StorageId = StorageIds.FarmHouseChest,
            ArtSource = Furniture.Cupboard,
            Position = new Vector2(2 * TileSize + 8, 2 * TileSize + 8), // (40, 40)
        });
        interactables.AddChild(new Door
        {
            Name = "FarmDoor",
            TargetMapId = MapIds.Farm,
            TargetSpawnId = "house_door",
            DrawPlaceholder = false,   // door_open is painted into the wall ring
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (120, 152)
        });
        AddChild(interactables);
    }
}
