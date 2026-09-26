using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// One guest room of the motor court, 9x7 tiles — which room it is comes from
/// <see cref="RoomNumber"/>, but each number is registered as its OWN map id
/// (motel handoff: rooms are not one map with a variant parameter, so story can
/// treat them separately forever). Rooms 1, 2 and 4 share a shell with swapped
/// dressing; room 3 differs — it is Pell's, three weeks into a one-night stay.
///
/// All four are locked from the west entry in Act I (StoryKeys.MotelRoomNOpen);
/// these rooms exist so an unlock is a flag stamp, never a build job.
/// </summary>
public partial class MotelRoomMap : InteriorMap
{
    public int RoomNumber { get; init; } = 1;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.MotelRoom(RoomNumber);
        base._EnterTree();
    }

    protected override void BuildDefaultLayout()
    {
        ResetLayout(9, 7, Floor.Board, Wall.Plaster, Wall.CornicePlank);

        switch (RoomNumber)
        {
            case 1:
                SetWall(3, 0, Wall.WindowDark);
                SetFloor(4, 3, Floor.RugA);
                break;
            case 2:
                SetWall(5, 0, Wall.WindowDark);
                SetFloor(4, 3, Floor.RugB);
                break;
            case 3:
                // Pell's room: the lit window.
                SetWall(3, 0, Wall.WindowLit);
                SetFloor(4, 3, Floor.RugB);
                SetFloor(4, 4, Floor.RugB);
                break;
            default:
                // The room Walt gave up on first: cracked plaster, one cobweb, no rug.
                SetWall(5, 0, Wall.PlasterCrack);
                SetWall(2, 0, Wall.WindowDark);
                SetDressing(7, 1, Dressing.Cobweb);
                break;
        }
    }

    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.MotelRoom(RoomNumber));

        // The shared shell: bed against the west wall, dresser opposite.
        recipe.Add(PlacementKinds.Furniture, "bed", 1, 1);
        recipe.Add(PlacementKinds.Furniture, "dresser", 7, 1);

        switch (RoomNumber)
        {
            case 1:
                recipe.Add(PlacementKinds.Furniture, "stool", 6, 4);
                break;
            case 2:
                recipe.Add(PlacementKinds.Furniture, "chair_side", 7, 4);
                break;
            case 3:
                // [KEVIN] Pell's room: the lit window and the sample bag. The radio
                // the locked door promises is HEARD, not yet drawn — the furniture
                // atlas has no radio piece; it lands with the next art pass. A
                // salesman's room kept like a display — tidy in a way that reads
                // wrong if you look twice, and nothing in here explains itself.
                recipe.Add(PlacementKinds.Furniture, "lamp", 2, 1);
                recipe.Add(PlacementKinds.Furniture, "books", 6, 1);
                recipe.Add(PlacementKinds.Furniture, "sack", 7, 4);
                break;
            default:
                recipe.Add(PlacementKinds.Furniture, "bucket", 6, 4);
                break;
        }

        recipe.Add(PlacementKinds.Spawn, "entry", 4, 5);     // (72, 88)
        recipe.Add(PlacementKinds.Spawn, "default", 4, 3);   // (72, 56)
        recipe.Add(PlacementKinds.Door, MapIds.WestEntry, 4, 6)
            .SetText(PlacementFields.Spawn, $"from_room{RoomNumber}");
        return recipe;
    }
}
