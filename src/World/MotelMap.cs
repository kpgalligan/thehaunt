using Godot;
using TheHaunt.Content;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The motel lobby, 14x10 tiles: a short registration desk in the north-west corner
/// with the till, the registry and one lamp on it, a rug that was nice once, and a
/// waiting bench nobody waits on — except lately Mr. Pell, who is in no hurry at all
/// (src/Content/Characters). Two windows on the back wall: one lit, one dark. Walt runs
/// four rooms and lights two, and the lobby says so without a word of dialogue.
///
/// Walt stands at the open end of his desk, so the Talk prompt never depends on
/// the probe stretching over furniture.
/// </summary>
public partial class MotelMap : InteriorMap
{
    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Motel;
        base._EnterTree();
    }

    protected override void BuildDefaultLayout()
    {
        ResetLayout(14, 10, Floor.Board, Wall.Plaster, Wall.CornicePlank);

        // Four rooms, two lamps: one window lit, one dark, and the key board between
        // (the lit lamps are the lobby and Pell's room 3 — see MotelRules.LitRoom).
        SetWall(2, 0, Wall.Plaque);
        SetWall(5, 0, Wall.WindowLit);
        SetWall(9, 0, Wall.WindowDark);

        // The registration desk — short, with an open end. The nook behind it stays
        // walkable; nothing back there is a secret.
        SetWallRun(1, 3, 4, Wall.Counter);

        // A rug that was nice once.
        SetFloor(6, 4, Floor.RugB);
        SetFloor(7, 4, Floor.RugB);
        SetFloor(6, 5, Floor.RugB);
        SetFloor(7, 5, Floor.RugB);

        // Somebody's cases by the door, and the corner barrel every lobby grows.
        SetWall(1, 8, Wall.Crate);
        SetWall(12, 8, Wall.Barrel);
    }

    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.Motel);
        // On the desk, not blocking: the blocker would replace the counter tile.
        recipe.Add(PlacementKinds.Furniture, "lamp", 1, 4).SetBool(PlacementFields.Blocks, false);
        recipe.Add(PlacementKinds.Furniture, "till", 2, 4).SetBool(PlacementFields.Blocks, false);
        // The registry: the one thing in this building kept precisely.
        recipe.Add(PlacementKinds.Furniture, "books", 3, 4).SetBool(PlacementFields.Blocks, false);
        recipe.Add(PlacementKinds.Furniture, "dresser", 1, 1);

        // The waiting corner.
        recipe.Add(PlacementKinds.Furniture, "bench", 8, 1);
        recipe.Add(PlacementKinds.Furniture, "books", 11, 1);

        recipe.Add(PlacementKinds.Spawn, "entry", 7, 8);     // (120, 136)
        recipe.Add(PlacementKinds.Spawn, "default", 7, 6);   // (120, 104)
        recipe.Add(PlacementKinds.Door, MapIds.WestEntry, 7, 9)
            .SetText(PlacementFields.Spawn, "from_motel");
        return recipe;
    }
}
