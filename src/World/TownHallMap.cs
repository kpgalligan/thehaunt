using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The meeting hall, 40x23 tiles — wider than the 30x17 viewport, so the player never
/// sees all of it at once, which makes it the one interior with anything to walk toward.
/// Checkered floor, a rug runner up the centre, pews in two blocks, clerks' desks at
/// both ends, and the long table on the runner where the placeholder podium stood.
///
/// The mayor's staging cell (20,6) is the row in front of the table, and the three
/// seated crew stage on the runner at row 12 — both unchanged. The door moved from row
/// 21 to row 22, where the handoff and its reference render both put it; the placeholder
/// doubled its south wall so the door could sit flush in row 21, and with the drawn
/// door_open tile that is no longer needed. Row 21 is floor now, and carries the
/// threshold.
///
/// The walls are wainscot plaster under a stone cornice, following the reference render.
/// The handoff's prose asks for stone walls; the render draws stone only in the cornice
/// row, which still leaves the hall the only interior that uses stone at all.
/// </summary>
public partial class TownHallMap : InteriorMap
{
    // Where the placeholder podium was; the long table now occupies it cell for cell.
    private const int TableLeft = 19, TableRow = 5;

    private static readonly int[] PewRows = { 9, 12, 15, 18 };
    private const int PewLeft = 11, PewRight = 24;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.TownHall;
        base._EnterTree();
    }

    protected override void BuildDefaultLayout()
    {
        ResetLayout(40, 23, Floor.Check, Wall.WainscotPlaster, Wall.CorniceStone);
        foreach (int x in new[] { 6, 12, 27, 33 })
            SetWall(x, 0, Wall.WindowLit);
        foreach (int x in new[] { 9, 30 })
            SetWall(x, 0, Wall.Plaque);

        // The runner: two columns from the table down to the row above the threshold,
        // which the build paints on the cell inside the door.
        for (int y = TableRow + 1; y < 21; y++)
        {
            SetFloor(19, y, Floor.RugA);
            SetFloor(20, y, Floor.RugA);
        }

        // The long table stands where the podium block did: (19..21, 4..5). Its base row
        // blocks through the furniture; the row behind it through Blockers.
        for (int x = TableLeft; x <= TableLeft + 2; x++)
            SetWall(x, TableRow - 1, Wall.Blocker);
    }

    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.TownHall);
        recipe.Add(PlacementKinds.Furniture, "long_table", TableLeft, TableRow);
        recipe.Add(PlacementKinds.Furniture, "banner", 16, TableRow);
        recipe.Add(PlacementKinds.Furniture, "banner", 24, TableRow);

        // Clerks at both ends.
        recipe.Add(PlacementKinds.Furniture, "desk", 3, 5);
        recipe.Add(PlacementKinds.Furniture, "chair_back", 4, 4);
        recipe.Add(PlacementKinds.Furniture, "desk", 34, 5);
        recipe.Add(PlacementKinds.Furniture, "chair_back", 35, 4);

        foreach (int y in new[] { 7, 12 })
        {
            recipe.Add(PlacementKinds.Furniture, "tall_shelf", 1, y);
            recipe.Add(PlacementKinds.Furniture, "tall_shelf", 38, y);
        }
        recipe.Add(PlacementKinds.Furniture, "candles", 7, 7);
        recipe.Add(PlacementKinds.Furniture, "candles", 32, 7);
        recipe.Add(PlacementKinds.Furniture, "books", 2, 10);
        recipe.Add(PlacementKinds.Furniture, "books", 37, 14);

        // Two blocks of four pews, either side of the aisle. The intro stages three crew
        // on the runner at row 12 between them; those cells stay clear.
        foreach (int y in PewRows)
        {
            recipe.Add(PlacementKinds.Furniture, "pew", PewLeft, y);
            recipe.Add(PlacementKinds.Furniture, "pew", PewRight, y);
        }

        recipe.Add(PlacementKinds.Furniture, "bench", 5, 20);
        recipe.Add(PlacementKinds.Furniture, "bench", 33, 20);

        recipe.Add(PlacementKinds.Spawn, "entry", 20, 19);   // (328, 312)
        recipe.Add(PlacementKinds.Door, MapIds.Town, 20, 22)
            .SetText(PlacementFields.Spawn, "from_hall");
        return recipe;
    }
}
