using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The gas station shop, 12x9 tiles: a stone floor, two shelf aisles of food and
/// sundries, a stack of crates that never gets unpacked, and the counter in the
/// east corner where Dennis serves out his sentence (docs/story/cast.md). One shelf
/// on the back wall is empty — restock day is whenever the truck feels like it.
/// Nothing here sells anything yet; the catalog comes with the economy pass.
///
/// Dennis stands at the open end of the counter, so the Talk prompt never
/// depends on the probe stretching over furniture.
/// </summary>
public partial class GasStationMap : InteriorMap
{
    protected override int Width => 12;
    protected override int Height => 9;
    protected override InteriorTiles.WallSet Walls { get; } =
        new(InteriorTiles.WallPlank, InteriorTiles.CornicePlaster);
    protected override int DoorX => 5;
    protected override int DoorY => 8;

    protected override Vector2I[] Floor { get; } =
    {
        InteriorTiles.FloorStone[0], InteriorTiles.FloorStone[1],
    };

    private const int CounterLeft = 8, CounterRight = 10, CounterRow = 4;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.GasStation;
        base._EnterTree();
    }

    protected override void Decorate()
    {
        // Back-wall stock, one gap: the empty shelf is mundane, not an omen.
        SetWall(1, 1, InteriorTiles.ShelfFull);
        SetWall(2, 1, InteriorTiles.ShelfFull);
        SetWall(3, 1, InteriorTiles.ShelfFull);
        SetWall(4, 1, InteriorTiles.ShelfEmpty);
        SetWall(3, 0, InteriorTiles.WindowLit);
        SetWall(9, 0, InteriorTiles.Plaque);

        // Two aisles. The threshold column stays clear all the way to the counter.
        AddFurniture(Furniture.WideShelf, 2, 3);
        AddFurniture(Furniture.WideShelf, 2, 5);

        // The counter, till toward the door; the area behind it is nobody's secret.
        AddCounter(CounterLeft, CounterRight, CounterRow);
        AddFurniture(Furniture.Till, 9, CounterRow, blocks: false);
        AddFurniture(Furniture.Sack, 10, 3);

        // Deliveries that never quite get shelved.
        AddFurniture(Furniture.Crates, 9, 7);
        AddFurniture(Furniture.Sack, 1, 7);
        SetWall(1, 6, InteriorTiles.Barrel);
    }

    protected override void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 7 * TileSize + 8), // (88, 120)
        });
        spawns.AddChild(new Marker2D
        {
            Name = "default",
            Position = new Vector2(5 * TileSize + 8, 5 * TileSize + 8), // (88, 88)
        });
        AddChild(spawns);
    }

    protected override void BuildInteractables()
    {
        AddChild(new Door
        {
            Name = "OutDoor",
            TargetMapId = MapIds.WestEntry,
            TargetSpawnId = "from_gas",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (88, 136)
        });
    }
}
