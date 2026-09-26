using Godot;
using TheHaunt.Content;
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

    private bool _built;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Barn;
        base._EnterTree();
    }

    /// <summary>The room as the file draws it: DERELICT, stains and webs and all.
    /// <see cref="ApplyRepairState"/> sweeps them.</summary>
    protected override void BuildDefaultLayout()
    {
        ResetLayout(16, 12, Floor.Dirt, Wall.Plank, Wall.CornicePlank);
        foreach (Vector2I cell in Hay)
            SetFloor(cell.X, cell.Y, Floor.Hay);

        // The loft runs along the first floor row, not the wall row — it is opaque
        // full-cell art, so it eats floor rather than replacing wall.
        SetWallRun(1, 5, 1, Wall.HayloftEdge);
        SetWallRun(10, 12, 1, Wall.RafterH);

        foreach (Vector2I cell in Stains)
            SetFloor(cell.X, cell.Y, Floor.Stain);
        foreach (Vector2I cell in Cobwebs)
            SetDressing(cell.X, cell.Y, Dressing.Cobweb);
    }

    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.Barn);
        recipe.Add(PlacementKinds.Furniture, "ladder", 6, 2);
        recipe.Add(PlacementKinds.Furniture, "stall", 2, 4);
        recipe.Add(PlacementKinds.Furniture, "stall", 5, 4);
        recipe.Add(PlacementKinds.Furniture, "haystack", 11, 4);
        recipe.Add(PlacementKinds.Furniture, "lamp", 8, 5);
        recipe.Add(PlacementKinds.Furniture, "tool_rack", 9, 6);
        recipe.Add(PlacementKinds.Furniture, "workbench", 9, 8);
        recipe.Add(PlacementKinds.Furniture, "crates", 2, 9);
        recipe.Add(PlacementKinds.Furniture, "sack", 5, 9);
        recipe.Add(PlacementKinds.Furniture, "bucket", 7, 9);
        recipe.Add(PlacementKinds.Furniture, "cart", 12, 10);

        recipe.Add(PlacementKinds.Spawn, "entry", 8, 10);    // (136, 168)
        recipe.Add(PlacementKinds.Spawn, "default", 7, 7);   // (120, 120)

        // The chest holding the previous owner's tools and seeds (StarterKit stocks it
        // on a new game — the farewell letter sends the player here). Beside the
        // workbench; no chest piece on the interior sheet, so the procedural placeholder.
        recipe.Add(PlacementKinds.Chest, StorageIds.BarnChest, 11, 8);
        recipe.Add(PlacementKinds.Door, MapIds.Farm, 8, 11)
            .SetText(PlacementFields.Spawn, "barn_door");
        return recipe;
    }

    protected override void OnBuilt(IReadOnlyList<MapPlacement> taken)
    {
        _built = true;
        ApplyRepairState();
    }

    /// <summary>
    /// Called on load and on every flag change through ApplyState, the same view-side
    /// model read the road blockade uses. Nothing durable lives on this node. The file's
    /// Stain floors and Cobweb dressing are the derelict state; anything past it sweeps
    /// the stains to dirt and pulls the webs down.
    /// </summary>
    private void ApplyRepairState()
    {
        // A flag can be stamped between _EnterTree (which registers this map with
        // WorldSim) and _Ready, and WorldSim repaints every registered map on a new flag.
        // OnBuilt runs this again once the layers exist.
        if (!_built)
            return;

        int state = BarnRules.StateOf(SaveService.Instance.Current);
        bool derelict = state <= BarnRules.Derelict;

        foreach (Vector2I cell in CellsOf(Floor.Stain))
            PaintFloor(cell.X, cell.Y, derelict ? Floor.Stain : Floor.Dirt);
        foreach (Vector2I cell in CellsOf(Dressing.Cobweb))
            PaintDressing(cell.X, cell.Y, derelict ? Dressing.Cobweb : null);
    }

    public override void ApplyState(MapState state) => ApplyRepairState();
}
