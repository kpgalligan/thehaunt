using Godot;

namespace TheHaunt.World;

/// <summary>
/// The town terrain TileSet, loaded once from the art handoff's Godot resource.
///
/// The .tres owns the atlas and the collision polygons (they ship with the art and
/// stay the artist's to change). What it cannot carry — the derived "walkable" custom
/// data and the collision-only blocker cell — is added by <see cref="TileSetTools"/>,
/// so town, farm and interiors all get it the same way.
/// </summary>
public static class TownTerrain
{
    public const string TileSetPath = "res://assets/sprites/town/thehaunt_terrain.tres";

    public const string WalkableData = TileSetTools.WalkableData;

    private static TileSet? _cached;

    /// <summary>Shared, immutable after the first call — maps only ever read it.</summary>
    public static TileSet Get() => _cached ??= Build();

    private static TileSet Build()
    {
        var tileSet = GD.Load<TileSet>(TileSetPath)
            ?? throw new InvalidOperationException($"Town terrain TileSet missing at '{TileSetPath}'.");

        TileSetTools.AddWalkableLayer(tileSet);

        // Collision-only cell: the atlas's row-3 tail is fully transparent, so this
        // blocks and reads as unwalkable while drawing nothing.
        TileSetTools.MakeBlocker((TileSetAtlasSource)tileSet.GetSource(0), TerrainTiles.Blocker);

        TileSetTools.DeriveWalkable(tileSet);
        return tileSet;
    }

    /// <summary>
    /// A private copy of the town TileSet, for a set that needs the woods edge and the
    /// blocker but is not the town — the farm draws the same forest boundary as the same
    /// diegetic map limit. Cache-ignoring, because <see cref="Get"/>'s instance is
    /// already owned by the town's TileSet and a source belongs to one set at a time.
    ///
    /// The whole TileSet comes back, not just its source: the source is a sub-resource of
    /// it, and handing out the source alone leaves the set unreferenced and collectable
    /// in the window before the caller re-parents it.
    /// </summary>
    public static TileSet LoadCopy() =>
        ResourceLoader.Load<TileSet>(TileSetPath, cacheMode: ResourceLoader.CacheMode.Ignore)
        ?? throw new InvalidOperationException($"Town terrain TileSet missing at '{TileSetPath}'.");
}
