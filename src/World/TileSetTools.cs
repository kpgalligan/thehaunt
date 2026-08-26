using Godot;

namespace TheHaunt.World;

/// <summary>
/// The three things every shipped-art TileSet needs that a .tres cannot carry, in one
/// place so town, farm and interiors cannot drift apart:
///   * the "walkable" custom data layer <see cref="MapRoot.IsStandable"/> reads, always
///     DERIVED from the resource's own collision and never hand-listed;
///   * a collision-only tile, so the Obstacles layer can block the ground under a
///     sprite-drawn facade, prop or furniture piece;
///   * a fully transparent one-tile atlas to hang that blocker on, for the sheets whose
///     every cell is drawn (only the town sheet has a spare transparent tail cell).
/// </summary>
public static class TileSetTools
{
    public const string WalkableData = "walkable";

    private static readonly Vector2[] FullTile =
    {
        new(-8, -8), new(8, -8), new(8, 8), new(-8, 8),
    };

    /// <summary>
    /// Adds the walkable layer, or finds it if this set already carries one. Call before
    /// <see cref="DeriveWalkable"/>.
    ///
    /// Idempotent on purpose: the .tres these run against is a PROCESS-cached resource,
    /// while the caches that gate the builders are ordinary C# statics. Anything that
    /// clears a static without clearing Godot's resource cache — an assembly reload while
    /// the editor is open, most of all — re-enters Build against a set that is already
    /// built. Adding a second layer here silently shifts what index 0 means; finding the
    /// existing one is the only behaviour that survives it.
    /// </summary>
    public static void AddWalkableLayer(TileSet tileSet)
    {
        for (int i = 0; i < tileSet.GetCustomDataLayersCount(); i++)
            if (tileSet.GetCustomDataLayerName(i) == WalkableData)
                return;

        tileSet.AddCustomDataLayer();
        int index = tileSet.GetCustomDataLayersCount() - 1;
        tileSet.SetCustomDataLayerName(index, WalkableData);
        tileSet.SetCustomDataLayerType(index, Variant.Type.Bool);
    }

    /// <summary>
    /// Registers <paramref name="coords"/> as a full-tile collision cell. Idempotent —
    /// re-stamping the collision on an existing blocker is a no-op, but CreateTile on one
    /// is an error. See <see cref="AddWalkableLayer"/> for why a second call happens.
    /// </summary>
    public static void MakeBlocker(TileSetAtlasSource source, Vector2I coords)
    {
        if (!source.HasTile(coords))
            source.CreateTile(coords);

        TileData blocker = source.GetTileData(coords, 0);
        blocker.SetCollisionPolygonsCount(0, 1);
        blocker.SetCollisionPolygonPoints(0, 0, FullTile);
    }

    /// <summary>
    /// A one-tile atlas of nothing, for sheets with no spare transparent cell. The
    /// caller adds it as its own source and paints tile (0,0) wherever a sprite needs
    /// the ground under it to block.
    /// </summary>
    public static TileSetAtlasSource TransparentBlockerSource()
    {
        var blank = Image.CreateEmpty(MapRoot.TileSize, MapRoot.TileSize, false, Image.Format.Rgba8);
        blank.Fill(new Color(0, 0, 0, 0));
        var source = new TileSetAtlasSource
        {
            Texture = ImageTexture.CreateFromImage(blank),
            TextureRegionSize = new Vector2I(MapRoot.TileSize, MapRoot.TileSize),
        };
        return source;
    }

    /// <summary>
    /// Stamps walkable = "this tile carries no collision" on every tile of every source.
    /// Run last, after all sources and blockers are in place.
    /// </summary>
    public static void DeriveWalkable(TileSet tileSet)
    {
        for (int s = 0; s < tileSet.GetSourceCount(); s++)
        {
            var source = (TileSetAtlasSource)tileSet.GetSource(tileSet.GetSourceId(s));
            for (int i = 0; i < source.GetTilesCount(); i++)
            {
                Vector2I coords = source.GetTileId(i);
                TileData data = source.GetTileData(coords, 0);
                data.SetCustomData(WalkableData, data.GetCollisionPolygonsCount(0) == 0);
            }
        }
    }
}
