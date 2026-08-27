using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Sam's salon, 12x9 tiles: a checkerboard floor, the styling chair on a small rug
/// in the middle of the room, and a stained-glass piece in the corner that nobody
/// asks about (docs/story/cast.md). Candles, a basin, a shelf of what might be
/// poetry. The haircut itself comes later; early game the room is Sam, and Sam is
/// texture.
///
/// Sam stands beside the chair on open floor, so the Talk prompt never depends
/// on the probe stretching over furniture.
/// </summary>
public partial class SalonMap : InteriorMap
{
    protected override int Width => 12;
    protected override int Height => 9;
    protected override InteriorTiles.WallSet Walls { get; } =
        new(InteriorTiles.WainscotPlaster, InteriorTiles.CornicePlank);
    protected override int DoorX => 6;
    protected override int DoorY => 8;

    protected override Vector2I[] Floor { get; } =
    {
        InteriorTiles.FloorCheckA, InteriorTiles.FloorCheckB,
    };

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Salon;
        base._EnterTree();
    }

    protected override void Decorate()
    {
        SetWall(3, 0, InteriorTiles.WindowLit);
        SetWall(8, 0, InteriorTiles.WindowLit);
        SetWall(6, 0, InteriorTiles.Plaque);

        // The chair, its rug, and the stool for whoever is next.
        SetFloor(5, 5, InteriorTiles.RugA);
        AddFurniture(Furniture.ChairFront, 5, 4);
        AddFurniture(Furniture.Stool, 7, 4);

        // Sam's shelf of supplies and the corner nobody asks about.
        AddFurniture(Furniture.Dresser, 1, 1);
        AddFurniture(Furniture.Candles, 2, 1);
        AddFurniture(Furniture.Stained, 10, 1);
        AddFurniture(Furniture.Books, 10, 6);
        AddFurniture(Furniture.Bucket, 1, 6);
    }

    protected override void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 7 * TileSize + 8), // (104, 120)
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
            TargetMapId = MapIds.EastEntry,
            TargetSpawnId = "from_salon",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (104, 136)
        });
    }
}
