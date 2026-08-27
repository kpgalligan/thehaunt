using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The room behind Billie's door, 16x12 tiles: the bar counter along the north side
/// with its back bar sealed by construction (the store's precedent), a hearth nook in
/// the north-east corner — the only fire in the room, which is all the light a dive
/// needs — and two tables in the south half where the shifts sit out their hours
/// (docs/story/cast.md). Shut windows on the south wall: whatever the hour outside,
/// in here it is always evening.
///
/// Billie and Bud stand on the OPEN side of the counter, not behind it, so the
/// Talk prompt never depends on the probe stretching over furniture.
/// </summary>
public partial class BilliesBarMap : InteriorMap
{
    protected override int Width => 16;
    protected override int Height => 12;
    protected override InteriorTiles.WallSet Walls { get; } =
        new(InteriorTiles.WallPlank, InteriorTiles.CorniceLog);
    protected override int DoorX => 7;
    protected override int DoorY => 11;

    // Worn-heavy plank: this floor has been drunk on for decades.
    protected override Vector2I[] Floor { get; } =
    {
        InteriorTiles.FloorPlankWorn, InteriorTiles.FloorPlank[0], InteriorTiles.FloorPlankWorn,
    };

    private const int CounterLeft = 1, CounterRight = 8, CounterRow = 3;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.BilliesBar;
        base._EnterTree();
    }

    protected override void Decorate()
    {
        // The bar. Its west end meets the wall ring; its east end stops short of the
        // hearth nook, and the shelf-and-barrel pair below closes the gap, so the back
        // bar x1-8, y1-2 is sealed by construction like the store's back room.
        AddCounter(CounterLeft, CounterRight, CounterRow);
        AddFurniture(Furniture.TallShelf, 9, 1);
        SetWall(9, 2, InteriorTiles.Barrel);

        // Back-bar stock, visible over the counter, forever out of reach.
        SetWall(2, 1, InteriorTiles.ShelfFull);
        SetWall(3, 1, InteriorTiles.ShelfFull);
        SetWall(5, 1, InteriorTiles.ShelfFull);
        SetWall(6, 1, InteriorTiles.ShelfFull);
        SetWall(1, 2, InteriorTiles.Barrel);
        SetWall(7, 2, InteriorTiles.Crate);

        // On the counter, not blocking: the blocker would replace the counter tile.
        AddFurniture(Furniture.Lamp, 3, CounterRow, blocks: false);
        AddFurniture(Furniture.Lamp, 6, CounterRow, blocks: false);
        AddFurniture(Furniture.Till, 8, CounterRow, blocks: false);

        // The hearth nook. AddHearth puts the mantel on row 1 and the fire on row 2 —
        // the nook's floor cells stay reachable around the counter's east end.
        AddHearth(11, 1);
        AddFurniture(Furniture.Stool, 13, 3);

        // The south half: two tables, the shifts' seats.
        AddFurniture(Furniture.Table, 3, 7);
        AddFurniture(Furniture.ChairBack, 3, 6);
        AddFurniture(Furniture.ChairSide, 5, 7);
        AddFurniture(Furniture.Table, 10, 7);
        AddFurniture(Furniture.ChairSide, 9, 7);
        AddFurniture(Furniture.ChairBack, 11, 6);
        AddFurniture(Furniture.Stool, 1, 8);
        AddFurniture(Furniture.Stool, 13, 8);
        AddFurniture(Furniture.Candles, 14, 5);

        // Day drinking is easier with the day shut out.
        SetWall(3, 11, InteriorTiles.WindowShut);
        SetWall(11, 11, InteriorTiles.WindowShut);
    }

    protected override void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 10 * TileSize + 8), // (120, 168)
        });
        spawns.AddChild(new Marker2D
        {
            Name = "default",
            Position = new Vector2(7 * TileSize + 8, 7 * TileSize + 8), // (120, 120)
        });
        AddChild(spawns);
    }

    protected override void BuildInteractables()
    {
        AddChild(new Door
        {
            Name = "OutDoor",
            TargetMapId = MapIds.Billies,
            TargetSpawnId = "from_bar",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (120, 184)
        });
    }
}
