using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The meeting hall, 40x23 tiles — wider than the 30x17 viewport, so the player never
/// sees all of it at once, which makes it the one interior with anything to walk toward.
/// Checkered floor, a rug runner up the centre, pews in two blocks, clerks' desks at
/// both ends, and the long table on the runner where the placeholder podium stood.
///
/// The mayor's staging cell (20,6) is the row in front of the table, and the three
/// seated crew stage on the runner at row 12 — both unchanged. The door moved from row
/// 21 to row 22, where the handoff and its reference render both put it; the placeholder
/// doubled its south wall so the door could sit flush in row 21, and with the drawn
/// door_open tile that is no longer needed. Row 21 is floor now, and carries the
/// threshold.
///
/// The walls are wainscot plaster under a stone cornice, following the reference render.
/// The handoff's prose asks for stone walls; the render draws stone only in the cornice
/// row, which still leaves the hall the only interior that uses stone at all.
/// </summary>
public partial class TownHallMap : InteriorMap
{
    protected override int Width => 40;
    protected override int Height => 23;
    protected override InteriorTiles.WallSet Walls => InteriorTiles.HallWalls;
    protected override int DoorX => 20;
    protected override int DoorY => 22;

    protected override Vector2I[] Floor { get; } =
    {
        InteriorTiles.FloorCheckA, InteriorTiles.FloorCheckB,
    };

    // Where the placeholder podium was; the long table now occupies it cell for cell.
    private const int TableLeft = 19, TableRow = 5;

    private static readonly int[] PewRows = { 9, 12, 15, 18 };
    private const int PewLeft = 11, PewRight = 24;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.TownHall;
        base._EnterTree();
    }

    protected override void Decorate()
    {
        foreach (int x in new[] { 6, 12, 27, 33 })
            SetWall(x, 0, InteriorTiles.WindowLit);
        foreach (int x in new[] { 9, 30 })
            SetWall(x, 0, InteriorTiles.Plaque);

        // The runner: two columns from the table down to the row above the threshold,
        // which the shell has already painted on the cell inside the door.
        for (int y = TableRow + 1; y < Height - 2; y++)
        {
            SetFloor(19, y, InteriorTiles.RugA);
            SetFloor(20, y, InteriorTiles.RugA);
        }

        // The long table stands where the podium block did: (19..21, 4..5).
        AddFurniture(Furniture.LongTable, TableLeft, TableRow);
        for (int x = TableLeft; x <= TableLeft + 2; x++)
            Block(x, TableRow - 1);
        AddFurniture(Furniture.Banner, 16, TableRow);
        AddFurniture(Furniture.Banner, 24, TableRow);

        // Clerks at both ends.
        AddFurniture(Furniture.Desk, 3, 5);
        AddFurniture(Furniture.ChairBack, 4, 4);
        AddFurniture(Furniture.Desk, 34, 5);
        AddFurniture(Furniture.ChairBack, 35, 4);

        foreach (int y in new[] { 7, 12 })
        {
            AddFurniture(Furniture.TallShelf, 1, y);
            AddFurniture(Furniture.TallShelf, 38, y);
        }
        AddFurniture(Furniture.Candles, 7, 7);
        AddFurniture(Furniture.Candles, 32, 7);
        AddFurniture(Furniture.Books, 2, 10);
        AddFurniture(Furniture.Books, 37, 14);

        // Two blocks of four pews, either side of the aisle. The intro stages three crew
        // on the runner at row 12 between them; those cells stay clear.
        foreach (int y in PewRows)
        {
            AddFurniture(Furniture.Pew, PewLeft, y);
            AddFurniture(Furniture.Pew, PewRight, y);
        }

        AddFurniture(Furniture.Bench, 5, 20);
        AddFurniture(Furniture.Bench, 33, 20);
    }

    protected override void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 19 * TileSize + 8), // (328, 312)
        });
        AddChild(spawns);
    }

    protected override void BuildInteractables()
    {
        AddChild(new Door
        {
            Name = "TownDoor",
            TargetMapId = MapIds.Town,
            TargetSpawnId = "from_hall",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (328, 360)
        });
    }
}
