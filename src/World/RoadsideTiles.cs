using Godot;

namespace TheHaunt.World;

/// <summary>
/// Named coordinates for the runtime-generated roadside sheet — asphalt, concrete and
/// the walkway curb — that <see cref="RoadsideTerrain"/> registers beside the shipped
/// town atlas. The motel handoff specifies these grounds as per-pixel mottle in exact
/// palette mixes and no shipped sheet carries them, so the source is authored from
/// that spec in code. Same act contract as <see cref="TerrainTiles"/>: every painted
/// cell goes through <see cref="ForAct"/>, so the dread variants stay a flag check.
/// </summary>
public static class RoadsideTiles
{
    public static readonly Vector2I[] Asphalt = { new(0, 0), new(1, 0), new(2, 0), new(3, 0) };
    public static readonly Vector2I[] Concrete = { new(4, 0), new(5, 0) };

    /// <summary>Concrete with the 2px kerb lip on its south edge — the walkway cell
    /// wherever the parking lot runs directly below it.</summary>
    public static readonly Vector2I ConcreteCurb = new(6, 0);

    public const int Columns = 7;

    /// <summary>Act I is the identity map, exactly like the town sheet's.</summary>
    public static Vector2I ForAct(Vector2I coords, TerrainTiles.Act act) => act switch
    {
        _ => coords,
    };
}
