using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The motel lobby, 14x10 tiles: a short registration desk in the north-west corner
/// with the till, the registry and one lamp on it, a rug that was nice once, and a
/// waiting bench nobody waits on — except lately Mr. Pell, who is in no hurry at all
/// (docs/story/cast.md). Two windows on the back wall: one lit, one dark. Walt runs
/// nine rooms and lights two, and the lobby says so without a word of dialogue.
///
/// Walt stands at the open end of his desk, so the Talk prompt never depends on
/// the probe stretching over furniture.
/// </summary>
public partial class MotelMap : InteriorMap
{
    protected override int Width => 14;
    protected override int Height => 10;
    protected override InteriorTiles.WallSet Walls { get; } =
        new(InteriorTiles.WallPlaster, InteriorTiles.CornicePlank);
    protected override int DoorX => 7;
    protected override int DoorY => 9;

    protected override Vector2I[] Floor { get; } =
    {
        InteriorTiles.FloorBoard[0], InteriorTiles.FloorBoard[1],
    };

    private const int DeskLeft = 1, DeskRight = 3, DeskRow = 4;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Motel;
        base._EnterTree();
    }

    protected override void Decorate()
    {
        // Nine rooms, two lamps: one window lit, one dark, and the key board between.
        SetWall(2, 0, InteriorTiles.Plaque);
        SetWall(5, 0, InteriorTiles.WindowLit);
        SetWall(9, 0, InteriorTiles.WindowDark);

        // The registration desk — short, with an open end. The nook behind it stays
        // walkable; nothing back there is a secret.
        AddCounter(DeskLeft, DeskRight, DeskRow);
        AddFurniture(Furniture.Lamp, 1, DeskRow, blocks: false);
        AddFurniture(Furniture.Till, 2, DeskRow, blocks: false);
        // The registry: the one thing in this building kept precisely.
        AddFurniture(Furniture.Books, 3, DeskRow, blocks: false);
        AddFurniture(Furniture.Dresser, 1, 1);

        // The waiting corner.
        AddFurniture(Furniture.Bench, 8, 1);
        AddFurniture(Furniture.Books, 11, 1);

        // A rug that was nice once.
        SetFloor(6, 4, InteriorTiles.RugB);
        SetFloor(7, 4, InteriorTiles.RugB);
        SetFloor(6, 5, InteriorTiles.RugB);
        SetFloor(7, 5, InteriorTiles.RugB);

        // Somebody's cases by the door, and the corner barrel every lobby grows.
        SetWall(1, 8, InteriorTiles.Crate);
        SetWall(12, 8, InteriorTiles.Barrel);
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
        AddChild(new Door
        {
            Name = "OutDoor",
            TargetMapId = MapIds.WestEntry,
            TargetSpawnId = "from_motel",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (120, 152)
        });
    }
}
