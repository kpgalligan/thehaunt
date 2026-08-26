using Godot;

namespace TheHaunt.World;

/// <summary>
/// The town terrain TileSet, loaded once from the art handoff's Godot resource.
///
/// The .tres owns the atlas and the collision polygons (they ship with the art and
/// stay the artist's to change). Two things it cannot carry are added here so there
/// is still exactly one source of truth:
///   * the "walkable" custom data layer <see cref="MapRoot.IsStandable"/> reads —
///     derived from the resource's own collision, never hand-listed;
///   * a collision-only tile on the atlas's transparent tail cell, so the Obstacles
///     layer can block the ground under sprite-drawn facades and props.
/// </summary>
public static class TownTerrain
{
    public const string TileSetPath = "res://assets/sprites/town/thehaunt_terrain.tres";

    public const string WalkableData = "walkable";

    private static TileSet? _cached;

    /// <summary>Shared, immutable after the first call — maps only ever read it.</summary>
    public static TileSet Get() => _cached ??= Build();

    private static TileSet Build()
    {
        var tileSet = GD.Load<TileSet>(TileSetPath)
            ?? throw new InvalidOperationException($"Town terrain TileSet missing at '{TileSetPath}'.");

        tileSet.AddCustomDataLayer();
        tileSet.SetCustomDataLayerName(0, WalkableData);
        tileSet.SetCustomDataLayerType(0, Variant.Type.Bool);

        var source = (TileSetAtlasSource)tileSet.GetSource(0);

        // Collision-only cell: the atlas's row-3 tail is fully transparent, so this
        // blocks and reads as unwalkable while drawing nothing.
        source.CreateTile(TerrainTiles.Blocker);
        var blocker = source.GetTileData(TerrainTiles.Blocker, 0);
        blocker.SetCollisionPolygonsCount(0, 1);
        blocker.SetCollisionPolygonPoints(0, 0, new[]
        {
            new Vector2(-8, -8), new Vector2(8, -8), new Vector2(8, 8), new Vector2(-8, 8),
        });

        for (int i = 0; i < source.GetTilesCount(); i++)
        {
            var coords = source.GetTileId(i);
            var data = source.GetTileData(coords, 0);
            data.SetCustomData(WalkableData, data.GetCollisionPolygonsCount(0) == 0);
        }

        return tileSet;
    }
}
