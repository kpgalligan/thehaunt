using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// One guest room of the motor court, 9x7 tiles — which room it is comes from
/// <see cref="RoomNumber"/>, but each number is registered as its OWN map id
/// (motel handoff: rooms are not one map with a variant parameter, so story can
/// treat them separately forever). Rooms 1, 2 and 4 share a shell with swapped
/// dressing; room 3 differs — it is Pell's, three weeks into a one-night stay.
///
/// All four are locked from the west entry in Act I (StoryKeys.MotelRoomNOpen);
/// these rooms exist so an unlock is a flag stamp, never a build job.
/// </summary>
public partial class MotelRoomMap : InteriorMap
{
    public int RoomNumber { get; init; } = 1;

    protected override int Width => 9;
    protected override int Height => 7;
    protected override InteriorTiles.WallSet Walls { get; } =
        new(InteriorTiles.WallPlaster, InteriorTiles.CornicePlank);
    protected override int DoorX => 4;
    protected override int DoorY => 6;

    protected override Vector2I[] Floor { get; } =
    {
        InteriorTiles.FloorBoard[0], InteriorTiles.FloorBoard[1],
    };

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.MotelRoom(RoomNumber);
        base._EnterTree();
    }

    protected override void Decorate()
    {
        // The shared shell: bed against the west wall, dresser opposite.
        AddFurniture(Furniture.Bed, 1, 1);
        AddFurniture(Furniture.Dresser, 7, 1);

        switch (RoomNumber)
        {
            case 1:
                SetWall(3, 0, InteriorTiles.WindowDark);
                SetFloor(4, 3, InteriorTiles.RugA);
                AddFurniture(Furniture.Stool, 6, 4);
                break;
            case 2:
                SetWall(5, 0, InteriorTiles.WindowDark);
                SetFloor(4, 3, InteriorTiles.RugB);
                AddFurniture(Furniture.ChairSide, 7, 4);
                break;
            case 3:
                // [KEVIN] Pell's room: the lit window and the sample bag. The radio
                // the locked door promises is HEARD, not yet drawn — the furniture
                // atlas has no radio piece; it lands with the next art pass. A
                // salesman's room kept like a display — tidy in a way that reads
                // wrong if you look twice, and nothing in here explains itself.
                SetWall(3, 0, InteriorTiles.WindowLit);
                SetFloor(4, 3, InteriorTiles.RugB);
                SetFloor(4, 4, InteriorTiles.RugB);
                AddFurniture(Furniture.Lamp, 2, 1);
                AddFurniture(Furniture.Books, 6, 1);
                AddFurniture(Furniture.Sack, 7, 4);
                break;
            default:
                // The room Walt gave up on first: cracked plaster, one cobweb, no rug.
                SetWall(5, 0, InteriorTiles.WallPlasterCrack);
                SetWall(2, 0, InteriorTiles.WindowDark);
                AddFurniture(Furniture.Bucket, 6, 4);
                AddCobweb(7, 1);
                break;
        }
    }

    protected override void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 5 * TileSize + 8), // (72, 88)
        });
        spawns.AddChild(new Marker2D
        {
            Name = "default",
            Position = new Vector2(DoorX * TileSize + 8, 3 * TileSize + 8), // (72, 56)
        });
        AddChild(spawns);
    }

    protected override void BuildInteractables()
    {
        AddChild(new Door
        {
            Name = "OutDoor",
            TargetMapId = MapIds.WestEntry,
            TargetSpawnId = $"from_room{RoomNumber}",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (72, 104)
        });
    }
}
