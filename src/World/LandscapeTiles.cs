using Godot;

namespace TheHaunt.World;

/// <summary>
/// Named coordinates for the runtime-generated landscape sheet — water, deep water and
/// the bush — that <see cref="RoadsideTerrain"/> registers as
/// <see cref="RoadsideTerrain.LandscapeSourceId"/>. No shipped sheet carries these yet,
/// so they are PLACEHOLDER art generated from the town handoff's palette hexes; every
/// tile is a full-tile blocker. Same act contract as <see cref="TerrainTiles"/>: every
/// painted cell goes through <see cref="ForAct"/>.
/// </summary>
public static class LandscapeTiles
{
    public static readonly Vector2I[] Water = { new(0, 0), new(1, 0) };
    public static readonly Vector2I[] DeepWater = { new(2, 0), new(3, 0) };
    public static readonly Vector2I[] Bush = { new(4, 0), new(5, 0) };

    public const int Columns = 6;

    /// <summary>The identity in EVERY act, deliberately: placeholder art has no dread
    /// variant set to swap to. Real art replaces this with a real mapping.</summary>
    public static Vector2I ForAct(Vector2I coords, TerrainTiles.Act act) => act switch
    {
        _ => coords,
    };
}
