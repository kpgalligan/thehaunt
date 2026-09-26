using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Sam's salon, 12x9 tiles: a checkerboard floor, the styling chair on a small rug
/// in the middle of the room, and a stained-glass piece in the corner that nobody
/// asks about (src/Content/Characters). Candles, a basin, a shelf of what might be
/// poetry. The haircut itself comes later; early game the room is Sam, and Sam is
/// texture.
///
/// Sam stands beside the chair on open floor, so the Talk prompt never depends
/// on the probe stretching over furniture.
/// </summary>
public partial class SalonMap : InteriorMap
{
    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Salon;
        base._EnterTree();
    }

    protected override void BuildDefaultLayout()
    {
        ResetLayout(12, 9, Floor.Check, Wall.WainscotPlaster, Wall.CornicePlank);
        SetWall(3, 0, Wall.WindowLit);
        SetWall(8, 0, Wall.WindowLit);
        SetWall(6, 0, Wall.Plaque);

        // The chair's rug.
        SetFloor(5, 5, Floor.RugA);
    }

    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.Salon);

        // The chair and the stool for whoever is next.
        recipe.Add(PlacementKinds.Furniture, "chair_front", 5, 4);
        recipe.Add(PlacementKinds.Furniture, "stool", 7, 4);

        // Sam's shelf of supplies and the corner nobody asks about.
        recipe.Add(PlacementKinds.Furniture, "dresser", 1, 1);
        recipe.Add(PlacementKinds.Furniture, "candles", 2, 1);
        recipe.Add(PlacementKinds.Furniture, "stained", 10, 1);
        recipe.Add(PlacementKinds.Furniture, "books", 10, 6);
        recipe.Add(PlacementKinds.Furniture, "bucket", 1, 6);

        recipe.Add(PlacementKinds.Spawn, "entry", 6, 7);     // (104, 120)
        recipe.Add(PlacementKinds.Spawn, "default", 6, 5);   // (104, 88)
        recipe.Add(PlacementKinds.Door, MapIds.EastEntry, 6, 8)
            .SetText(PlacementFields.Spawn, "from_salon");
        return recipe;
    }
}
