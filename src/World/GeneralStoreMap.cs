using Godot;
using TheHaunt.Content;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The general store interior, 14x10 tiles, dressed from the handoff's reference room.
/// Plank wainscot under a plastered cornice, and the wall-to-wall counter that seals the
/// back area y1-3 so the shopkeeper is unreachable by construction — the ShopCounter
/// Area2D spanning the counter strip is the shop entry point.
///
/// The reference draws a four-cell counter; the map keeps its twelve, because the sealed
/// back room is a gameplay contract and the counter grammar extends to any width:
/// panelled ends, plain middle. The till moves to sit directly in front of the
/// shopkeeper's scheduled cell (6,3) rather than the reference's (4,4).
/// </summary>
public partial class GeneralStoreMap : InteriorMap
{
    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.GeneralStore;
        base._EnterTree();
    }

    protected override void BuildDefaultLayout()
    {
        ResetLayout(14, 10, Floor.Plank, Wall.WainscotPlank, Wall.CornicePlaster);
        SetWall(3, 0, Wall.WindowLit);
        SetWall(10, 0, Wall.WindowLit);
        SetWall(7, 0, Wall.Plaque);

        // Counter row, WALL-TO-WALL (x1-12 at y4, meeting the ring on both sides): the
        // back area y1-3 is sealed by construction.
        SetWallRun(1, 12, 4, Wall.Counter);

        // Against the south wall, not a row up: a barrel at (1,7) and a crate at (2,7)
        // pin (1,8) and (2,8) between themselves, the wall and the crate stack, and two
        // cells of the customer's half become floor nobody can reach.
        SetWall(1, 8, Wall.Barrel);
        SetWall(2, 8, Wall.Crate);
        SetWall(12, 7, Wall.Barrel);
    }

    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.GeneralStore);

        // Back-room stock — visible over the counter, unreachable behind it.
        recipe.Add(PlacementKinds.Furniture, "books", 5, 1);
        recipe.Add(PlacementKinds.Furniture, "wide_shelf", 8, 2);
        recipe.Add(PlacementKinds.Furniture, "tall_shelf", 1, 3);
        recipe.Add(PlacementKinds.Furniture, "tall_shelf", 12, 3);
        // The till sits ON the counter, directly in front of the shopkeeper's scheduled
        // cell (6,3). It must NOT take a blocker of its own: the blocker replaces the
        // Obstacles cell, and that cell is the counter tile — the counter would gain a
        // hole behind the till. The counter already blocks.
        recipe.Add(PlacementKinds.Furniture, "till", 6, 4).SetBool(PlacementFields.Blocks, false);

        // The player's half of the room.
        recipe.Add(PlacementKinds.Furniture, "seed_bins", 9, 5);
        recipe.Add(PlacementKinds.Furniture, "crates", 3, 8);
        recipe.Add(PlacementKinds.Furniture, "sack", 11, 7);

        recipe.Add(PlacementKinds.Spawn, "entry", 7, 8);     // (120, 136)
        recipe.Add(PlacementKinds.Spawn, "default", 7, 6);   // (120, 104)

        // The shop entry point: the strip over the counter x1-12, y4.
        MapPlacement counter = recipe.Add(PlacementKinds.ShopCounter, ShopCatalog.GeneralStore, 1, 4);
        counter.SetInt(PlacementFields.Width, 12);
        counter.SetInt(PlacementFields.Height, 1);

        recipe.Add(PlacementKinds.Door, MapIds.Town, 7, 9)
            .SetText(PlacementFields.Spawn, "from_store");
        return recipe;
    }
}
