using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The gas station shop, 12x9 tiles: a stone floor, two shelf aisles of food and
/// sundries, a stack of crates that never gets unpacked, and the counter in the
/// east corner where Dennis serves out his sentence (src/Content/Characters). One shelf
/// on the back wall is empty — restock day is whenever the truck feels like it.
/// Nothing here sells anything yet; the catalog comes with the economy pass.
///
/// Dennis stands at the open end of the counter, so the Talk prompt never
/// depends on the probe stretching over furniture.
/// </summary>
public partial class GasStationMap : InteriorMap
{
    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.GasStation;
        base._EnterTree();
    }

    protected override void BuildDefaultLayout()
    {
        ResetLayout(12, 9, Floor.Stone, Wall.Plank, Wall.CornicePlaster);

        // Back-wall stock, one gap: the empty shelf is mundane, not an omen.
        SetWall(1, 1, Wall.ShelfFull);
        SetWall(2, 1, Wall.ShelfFull);
        SetWall(3, 1, Wall.ShelfFull);
        SetWall(4, 1, Wall.ShelfEmpty);
        SetWall(3, 0, Wall.WindowLit);
        SetWall(9, 0, Wall.Plaque);

        // The counter; the area behind it is nobody's secret.
        SetWallRun(8, 10, 4, Wall.Counter);

        SetWall(1, 6, Wall.Barrel);
    }

    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.GasStation);

        // Two aisles. The threshold column stays clear all the way to the counter.
        recipe.Add(PlacementKinds.Furniture, "wide_shelf", 2, 3);
        recipe.Add(PlacementKinds.Furniture, "wide_shelf", 2, 5);

        // Till toward the door, on the counter.
        recipe.Add(PlacementKinds.Furniture, "till", 9, 4).SetBool(PlacementFields.Blocks, false);
        recipe.Add(PlacementKinds.Furniture, "sack", 10, 3);

        // Deliveries that never quite get shelved.
        recipe.Add(PlacementKinds.Furniture, "crates", 9, 7);
        recipe.Add(PlacementKinds.Furniture, "sack", 1, 7);

        recipe.Add(PlacementKinds.Spawn, "entry", 5, 7);     // (88, 120)
        recipe.Add(PlacementKinds.Spawn, "default", 5, 5);   // (88, 88)
        recipe.Add(PlacementKinds.Door, MapIds.WestEntry, 5, 8)
            .SetText(PlacementFields.Spawn, "from_gas");
        return recipe;
    }
}
