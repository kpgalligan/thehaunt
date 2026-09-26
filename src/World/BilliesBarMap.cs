using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The room behind Billie's door, 16x12 tiles: the bar counter along the north side
/// with its back bar sealed by construction (the store's precedent), a hearth nook in
/// the north-east corner — the only fire in the room, which is all the light a dive
/// needs — and two tables in the south half where the shifts sit out their hours
/// (src/Content/Characters). Shut windows on the south wall: whatever the hour outside,
/// in here it is always evening.
///
/// Billie and Bud stand on the OPEN side of the counter, not behind it, so the
/// Talk prompt never depends on the probe stretching over furniture.
/// </summary>
public partial class BilliesBarMap : InteriorMap
{
    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.BilliesBar;
        base._EnterTree();
    }

    protected override void BuildDefaultLayout()
    {
        // Worn-heavy plank: this floor has been drunk on for decades.
        ResetLayout(16, 12, Floor.PlankWorn, Wall.Plank, Wall.CorniceLog);

        // The bar. Its west end meets the wall ring; its east end stops short of the
        // hearth nook, and the shelf-and-barrel pair beside it closes the gap, so the back
        // bar x1-8, y1-2 is sealed by construction like the store's back room.
        SetWallRun(1, 8, 3, Wall.Counter);
        SetWall(9, 2, Wall.Barrel);

        // Back-bar stock, visible over the counter, forever out of reach.
        SetWall(2, 1, Wall.ShelfFull);
        SetWall(3, 1, Wall.ShelfFull);
        SetWall(5, 1, Wall.ShelfFull);
        SetWall(6, 1, Wall.ShelfFull);
        SetWall(1, 2, Wall.Barrel);
        SetWall(7, 2, Wall.Crate);

        // The hearth nook: the mantel on row 1 and the fire on row 2 — the nook's floor
        // cells stay reachable around the counter's east end.
        SetWallRun(11, 13, 1, Wall.Hearth);
        SetWall(12, 2, Wall.HearthFire);

        // Day drinking is easier with the day shut out.
        SetWall(3, 11, Wall.WindowShut);
        SetWall(11, 11, Wall.WindowShut);
    }

    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.BilliesBar);
        recipe.Add(PlacementKinds.Furniture, "tall_shelf", 9, 1);

        // On the counter, not blocking: the blocker would replace the counter tile.
        recipe.Add(PlacementKinds.Furniture, "lamp", 3, 3).SetBool(PlacementFields.Blocks, false);
        recipe.Add(PlacementKinds.Furniture, "lamp", 6, 3).SetBool(PlacementFields.Blocks, false);
        recipe.Add(PlacementKinds.Furniture, "till", 8, 3).SetBool(PlacementFields.Blocks, false);

        recipe.Add(PlacementKinds.Furniture, "stool", 13, 3);

        // The south half: two tables, the shifts' seats.
        recipe.Add(PlacementKinds.Furniture, "table", 3, 7);
        recipe.Add(PlacementKinds.Furniture, "chair_back", 3, 6);
        recipe.Add(PlacementKinds.Furniture, "chair_side", 5, 7);
        recipe.Add(PlacementKinds.Furniture, "table", 10, 7);
        recipe.Add(PlacementKinds.Furniture, "chair_side", 9, 7);
        recipe.Add(PlacementKinds.Furniture, "chair_back", 11, 6);
        recipe.Add(PlacementKinds.Furniture, "stool", 1, 8);
        recipe.Add(PlacementKinds.Furniture, "stool", 13, 8);
        recipe.Add(PlacementKinds.Furniture, "candles", 14, 5);

        recipe.Add(PlacementKinds.Spawn, "entry", 7, 10);    // (120, 168)
        recipe.Add(PlacementKinds.Spawn, "default", 7, 7);   // (120, 120)
        recipe.Add(PlacementKinds.Door, MapIds.Billies, 7, 11)
            .SetText(PlacementFields.Spawn, "from_bar");
        return recipe;
    }
}
