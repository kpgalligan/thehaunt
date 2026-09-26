using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The farmhouse interior, 14x10 tiles, dressed from the farm/interiors handoff's
/// reference room. Log walls, plank floor, a hearth on the north side with the bed
/// beside it, and the stove and cupboard along the west end of the same wall.
///
/// Every gameplay position is exactly where it was: bed (12,2)-(12,3), chest (2,2),
/// table (6,4)+(7,4), door (7,9). The chest is drawn as the cupboard the reference room
/// puts on its cell, and the bed is the sheet's bed — both keep their own collision and
/// their own interaction, they just stopped being coloured rectangles.
/// </summary>
public partial class FarmHouseMap : InteriorMap
{
    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.FarmHouse;
        base._EnterTree();
    }

    protected override void BuildDefaultLayout()
    {
        // The reference's three-step diagonal stagger — plank a, plank b, then a worn board.
        ResetLayout(14, 10, Floor.PlankStagger, Wall.Log, Wall.CornicePlank);

        // Lit windows in the cornice row. They are drawn on a cream plaster ground, so
        // against the log wall they read as whitewashed trim around the glass — which is
        // how the reference room draws them too.
        SetWall(3, 0, Wall.WindowLit);
        SetWall(10, 0, Wall.WindowLit);

        // hearth_l/c/r on (10..12, 1), fire at (11, 2): the fire tile has no lintel of its
        // own, so it only ever goes underneath (handoff §5).
        SetWallRun(10, 12, 1, Wall.Hearth);
        SetWall(11, 2, Wall.HearthFire);

        // The rug is floor, not furniture: red on the left column, plum on the right.
        for (int y = 6; y <= 7; y++)
        {
            SetFloor(5, y, Floor.RugA);
            SetFloor(6, y, Floor.RugB);
        }
    }

    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.FarmHouse);
        recipe.Add(PlacementKinds.Furniture, "stove", 3, 2);
        recipe.Add(PlacementKinds.Furniture, "pot", 4, 2);
        recipe.Add(PlacementKinds.Furniture, "chair_side", 5, 4);
        recipe.Add(PlacementKinds.Furniture, "table", 6, 4);
        recipe.Add(PlacementKinds.Furniture, "chair_back", 8, 4);
        recipe.Add(PlacementKinds.Furniture, "lamp", 9, 6);
        recipe.Add(PlacementKinds.Furniture, "bucket", 1, 7);
        recipe.Add(PlacementKinds.Furniture, "sack", 12, 7);

        recipe.Add(PlacementKinds.Spawn, "entry", 7, 8);     // (120, 136)
        recipe.Add(PlacementKinds.Spawn, "default", 6, 5);   // (104, 88)

        // The bed that used to sit outdoors on the farm — same class, new home, and now
        // the 16x32 piece from the sheet standing exactly on its two cells.
        recipe.Add(PlacementKinds.Bed, "bed", 12, 3);
        // The reference room draws the storage on its cell as a cupboard; the chest's
        // contents still live in GameData.Storages.
        recipe.Add(PlacementKinds.Chest, StorageIds.FarmHouseChest, 2, 2)
            .SetText(PlacementFields.Art, "cupboard");
        recipe.Add(PlacementKinds.Door, MapIds.Farm, 7, 9)
            .SetText(PlacementFields.Spawn, "house_door");
        return recipe;
    }
}
