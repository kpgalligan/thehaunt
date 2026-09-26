using Godot;

namespace TheHaunt.World;

/// <summary>
/// Named atlas coordinates for the interior sheet (assets/sprites/interior/interior.png,
/// 16 columns x 4 rows of 16px tiles), from the farm/interiors handoff §5.
///
/// Rooms never name these directly: a room's file names its cells by
/// <see cref="InteriorMap.Floor"/>, <see cref="InteriorMap.Wall"/> and
/// <see cref="InteriorMap.Dressing"/>, and <see cref="InteriorMap"/> maps each name to
/// its tile here (floor variants, counter and hearth pieces, door_open and threshold are
/// derived there). Row 0 is floors (walkable), row 1 is the wall lower course and its
/// openings, row 2 is the upper course and ceiling, row 3 is fixtures.
/// </summary>
public static class InteriorTiles
{
    /// <summary>Same escalation seam as the exterior sheets: one switch, no re-laid rooms.</summary>
    public enum Act { One }

    public static Vector2I ForAct(Vector2I coords, Act act) => act switch
    {
        _ => coords,
    };

    // ---- Row 0: floors (all walkable) ----------------------------------
    public static readonly Vector2I[] FloorPlank = { new(0, 0), new(1, 0) };
    public static readonly Vector2I FloorPlankWorn = new(2, 0);
    public static readonly Vector2I[] FloorStone = { new(3, 0), new(4, 0) };
    public static readonly Vector2I FloorDirt = new(5, 0);
    public static readonly Vector2I FloorHay = new(6, 0);
    public static readonly Vector2I RugA = new(7, 0);
    public static readonly Vector2I RugB = new(8, 0);
    public static readonly Vector2I[] FloorBoard = { new(9, 0), new(10, 0) };
    public static readonly Vector2I FloorCheckA = new(11, 0);
    public static readonly Vector2I FloorCheckB = new(12, 0);
    public static readonly Vector2I FloorStain = new(13, 0);
    public static readonly Vector2I FloorDark = new(14, 0);

    /// <summary>Goes on the floor cell just inside a door (handoff §5).</summary>
    public static readonly Vector2I Threshold = new(15, 0);

    // ---- Row 1: wall lower course and openings (solid but door_open) ---
    public static readonly Vector2I WallPlaster = new(0, 1);
    public static readonly Vector2I WallPlank = new(1, 1);
    public static readonly Vector2I WallStone = new(2, 1);
    public static readonly Vector2I WallLog = new(3, 1);
    public static readonly Vector2I WainscotPlaster = new(4, 1);
    public static readonly Vector2I WainscotPlank = new(5, 1);
    public static readonly Vector2I WallPlasterCrack = new(6, 1);
    public static readonly Vector2I WallStoneCrack = new(7, 1);
    public static readonly Vector2I WallCornerL = new(8, 1);
    public static readonly Vector2I WallCornerR = new(9, 1);
    public static readonly Vector2I WindowDark = new(10, 1);
    public static readonly Vector2I WindowLit = new(11, 1);
    public static readonly Vector2I WindowShut = new(12, 1);
    public static readonly Vector2I DoorClosed = new(13, 1);
    public static readonly Vector2I DoorOpen = new(14, 1);      // the one walkable row-1 tile
    public static readonly Vector2I WallBeam = new(15, 1);

    // ---- Row 2: upper course, ceiling, rafters (all solid) -------------
    public static readonly Vector2I CornicePlaster = new(0, 2);
    public static readonly Vector2I CornicePlank = new(1, 2);
    public static readonly Vector2I CorniceStone = new(2, 2);
    public static readonly Vector2I CorniceLog = new(3, 2);
    public static readonly Vector2I[] Ceiling = { new(4, 2), new(5, 2) };
    public static readonly Vector2I RafterH = new(6, 2);
    public static readonly Vector2I RafterV = new(7, 2);
    public static readonly Vector2I HayloftEdge = new(8, 2);
    public static readonly Vector2I WallRail = new(9, 2);
    public static readonly Vector2I UpperPlaster = new(10, 2);
    public static readonly Vector2I UpperPlank = new(11, 2);
    public static readonly Vector2I UpperStone = new(12, 2);
    public static readonly Vector2I UpperLog = new(13, 2);
    public static readonly Vector2I Plaque = new(14, 2);
    public static readonly Vector2I LanternBracket = new(15, 2);

    // ---- Row 3: fixtures and storage (all solid but cobweb) ------------
    public static readonly Vector2I StairUp = new(0, 3);
    public static readonly Vector2I StairDown = new(1, 3);
    public static readonly Vector2I HearthL = new(2, 3);
    public static readonly Vector2I HearthC = new(3, 3);
    public static readonly Vector2I HearthR = new(4, 3);
    public static readonly Vector2I HearthFire = new(5, 3);
    public static readonly Vector2I CounterL = new(6, 3);
    public static readonly Vector2I CounterC = new(7, 3);
    public static readonly Vector2I CounterR = new(8, 3);
    public static readonly Vector2I ShelfEmpty = new(9, 3);
    public static readonly Vector2I ShelfFull = new(10, 3);
    public static readonly Vector2I Barrel = new(11, 3);
    public static readonly Vector2I Crate = new(12, 3);
    public static readonly Vector2I Sack = new(13, 3);
    public static readonly Vector2I HayBale = new(14, 3);
    public static readonly Vector2I Cobweb = new(15, 3);        // the one walkable row-3 tile
}
