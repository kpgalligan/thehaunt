using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The barn interior, 16x12 tiles, laid out from the handoff's reference room: dirt
/// floor under a hayloft edge with the ladder beside it, two stall dividers, a workbench
/// and tool rack, a cart, crates and a haystack.
///
/// The room is drawn in the derelict state — cobwebs and floor stains — and repairs by
/// subtraction: the restored barn is the SAME layout with the stains swept and the webs
/// pulled down. That is the whole of the difference the shipped art can express, because
/// the lamp on the sheet is already lit and there is no unlit twin of it.
///
/// The north row takes cornice_plank rather than the reference's cornice_log: log's
/// dominant colour is the same brown as floor_dirt, so the back wall dissolves into the
/// floor. Plank matches the side walls and reads.
/// </summary>
public partial class BarnMap : InteriorMap
{
    protected override int Width => 16;
    protected override int Height => 12;
    protected override InteriorTiles.WallSet Walls => InteriorTiles.BarnWalls;
    protected override int DoorX => 8;
    protected override int DoorY => 11;

    protected override Vector2I[] Floor { get; } = { InteriorTiles.FloorDirt };

    private static readonly Vector2I[] Hay =
    {
        new(4, 2), new(9, 3), new(10, 3), new(4, 5), new(1, 6), new(4, 6), new(5, 6),
        new(12, 6), new(13, 6), new(14, 6), new(9, 7), new(12, 7), new(7, 8), new(3, 9),
        new(5, 9), new(10, 10), new(12, 10),
    };

    // Swept when the barn is repaired — the only dressing the three states differ by.
    private static readonly Vector2I[] Stains =
    {
        new(6, 2), new(7, 2), new(14, 2), new(11, 3), new(14, 3), new(7, 5), new(7, 6),
        new(9, 6), new(3, 7), new(11, 9),
    };

    private static readonly Vector2I[] Cobwebs = { new(1, 1), new(14, 1), new(14, 5) };

    private bool _decorated;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Barn;
        base._EnterTree();
    }

    protected override void Decorate()
    {
        foreach (Vector2I cell in Hay)
            SetFloor(cell.X, cell.Y, InteriorTiles.FloorHay);

        // The loft runs along the first floor row, not the wall row — it is opaque
        // full-cell art, so it eats floor rather than replacing wall.
        for (int x = 1; x <= 5; x++)
            SetWall(x, 1, InteriorTiles.HayloftEdge);
        for (int x = 10; x <= 12; x++)
            SetWall(x, 1, InteriorTiles.RafterH);

        AddFurniture(Furniture.Ladder, 6, 2);
        AddFurniture(Furniture.Stall, 2, 4);
        AddFurniture(Furniture.Stall, 5, 4);
        AddFurniture(Furniture.Haystack, 11, 4);
        AddFurniture(Furniture.Lamp, 8, 5);
        AddFurniture(Furniture.ToolRack, 9, 6);
        AddFurniture(Furniture.Workbench, 9, 8);
        AddFurniture(Furniture.Crates, 2, 9);
        AddFurniture(Furniture.Sack, 5, 9);
        AddFurniture(Furniture.Bucket, 7, 9);
        AddFurniture(Furniture.Cart, 12, 10);

        _decorated = true;
        ApplyRepairState();
    }

    /// <summary>
    /// Called on load and on every flag change through ApplyState, the same view-side
    /// model read the road blockade uses. Nothing durable lives on this node.
    /// </summary>
    private void ApplyRepairState()
    {
        // A flag can be stamped between _EnterTree (which registers this map with
        // WorldSim) and _Ready, and WorldSim repaints every registered map on a new flag.
        // Decorate runs this again once the layers exist.
        if (!_decorated)
            return;

        int state = BarnRules.StateOf(SaveService.Instance.Current);
        bool derelict = state <= BarnRules.Derelict;

        foreach (Vector2I cell in Stains)
            SetFloor(cell.X, cell.Y, derelict ? InteriorTiles.FloorStain : InteriorTiles.FloorDirt);
        foreach (Vector2I cell in Cobwebs)
        {
            if (derelict) AddCobweb(cell.X, cell.Y);
            else ClearDressing(cell.X, cell.Y);
        }
    }

    public override void ApplyState(MapState state) => ApplyRepairState();

    protected override void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 10 * TileSize + 8), // (136, 168)
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
            Name = "YardDoor",
            TargetMapId = MapIds.Farm,
            TargetSpawnId = "barn_door",
            DrawPlaceholder = false,
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (136, 184)
        });
    }
}
