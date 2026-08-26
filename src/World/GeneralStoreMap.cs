using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The general store interior, 14x10 tiles, dressed from the handoff's reference room.
/// Plank wainscot under a plastered cornice, and the wall-to-wall counter that seals the
/// back area y1-3 so the shopkeeper is unreachable by construction — the ShopCounter
/// Area2D spanning the counter strip is the shop entry point.
///
/// The reference draws a four-cell counter; the map keeps its twelve, because the sealed
/// back room is a gameplay contract and the counter grammar extends to any width:
/// panelled ends, plain middle. The till moves to sit directly in front of the
/// shopkeeper's scheduled cell (6,3) rather than the reference's (4,4).
/// </summary>
public partial class GeneralStoreMap : InteriorMap
{
    protected override int Width => 14;
    protected override int Height => 10;
    protected override InteriorTiles.WallSet Walls => InteriorTiles.StoreWalls;
    protected override int DoorX => 7;
    protected override int DoorY => 9;

    protected override Vector2I[] Floor { get; } =
    {
        InteriorTiles.FloorPlank[0], InteriorTiles.FloorPlank[1],
    };

    private const int CounterLeft = 1, CounterRight = 12, CounterRow = 4;
    private const int ShopkeeperX = 6;   // NpcSchedules stages the shopkeeper at (6,3)

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.GeneralStore;
        base._EnterTree();
    }

    protected override void Decorate()
    {
        SetWall(3, 0, InteriorTiles.WindowLit);
        SetWall(10, 0, InteriorTiles.WindowLit);
        SetWall(DoorX, 0, InteriorTiles.Plaque);

        // Counter row, WALL-TO-WALL (x1-12 at y4, meeting the ring on both sides): the
        // back area y1-3 is sealed by construction.
        AddCounter(CounterLeft, CounterRight, CounterRow);

        // Back-room stock — visible over the counter, unreachable behind it.
        AddFurniture(Furniture.Books, 5, 1);
        AddFurniture(Furniture.WideShelf, 8, 2);
        AddFurniture(Furniture.TallShelf, 1, 3);
        AddFurniture(Furniture.TallShelf, 12, 3);
        // The till sits ON the counter. It must NOT take a blocker of its own: the
        // blocker replaces the Obstacles cell, and that cell is the counter tile — the
        // counter would gain a hole behind the till. The counter already blocks.
        AddFurniture(Furniture.Till, ShopkeeperX, CounterRow, blocks: false);

        // The player's half of the room.
        AddFurniture(Furniture.SeedBins, 9, 5);
        AddFurniture(Furniture.Crates, 3, 8);
        AddFurniture(Furniture.Sack, 11, 7);
        // Against the south wall, not a row up: a barrel at (1,7) and a crate at (2,7)
        // pin (1,8) and (2,8) between themselves, the wall and the crate stack, and two
        // cells of the customer's half become floor nobody can reach.
        SetWall(1, 8, InteriorTiles.Barrel);
        SetWall(2, 8, InteriorTiles.Crate);
        SetWall(12, 7, InteriorTiles.Barrel);
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
            Position = new Vector2(7 * TileSize + 8, 6 * TileSize + 8), // (120, 104)
        });
        AddChild(spawns);
    }

    protected override void BuildInteractables()
    {
        // ShopCounter has no sprite and no blocker of its own — the counter tiles are
        // the visual and the collision; the owning map supplies the shape covering them
        // (MapExit precedent).
        var counter = new ShopCounter
        {
            Name = "ShopCounter",
            Position = new Vector2(112, 72), // center of the counter strip x1-12, y4
        };
        counter.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(192, 16) },
        });
        AddChild(counter);

        AddChild(new Door
        {
            Name = "TownDoor",
            TargetMapId = MapIds.Town,
            TargetSpawnId = "from_store",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (120, 152)
        });
    }
}
