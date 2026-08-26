using Godot;

namespace TheHaunt.World;

/// <summary>
/// The farm's ground TileSet: the farm sheet's own 64 tiles plus a private copy of the
/// town atlas, because the farm's map limit is the SAME forest as the town's — one
/// diegetic rule, one set of tiles — and that atlas is also where the collision-only
/// blocker cell lives.
///
/// Loaded once and shared; the walkable data is derived from the resources' own
/// collision by <see cref="TileSetTools"/>, never hand-listed.
/// </summary>
public static class FarmTerrain
{
    public const string TileSetPath = "res://assets/sprites/farm/thehaunt_farm.tres";

    /// <summary>Source id of the farm sheet — soil, pasture, fences, paths.</summary>
    public const int FarmSource = 0;

    /// <summary>Source id of the town atlas copy — the woods edge and the blocker.</summary>
    public const int TownSource = 1;

    private static TileSet? _cached;

    // The donor set the town atlas was lifted out of. Kept alive for the life of the
    // process: it is empty afterwards, but letting it go would be betting on when a
    // sub-resource's last owner is allowed to disappear.
    private static TileSet? _donor;

    public static TileSet Get() => _cached ??= Build();

    private static TileSet Build()
    {
        var tileSet = GD.Load<TileSet>(TileSetPath)
            ?? throw new InvalidOperationException($"Farm terrain TileSet missing at '{TileSetPath}'.");

        // The .tres comes back from Godot's process-wide resource cache, which outlives
        // this class's _cached static — a C# assembly reload clears one and not the other.
        // If the merge already happened, adopt it rather than re-merging or throwing.
        TileSetAtlasSource woods;
        if (tileSet.HasSource(TownSource))
        {
            woods = (TileSetAtlasSource)tileSet.GetSource(TownSource);
        }
        else
        {
            _donor = TownTerrain.LoadCopy();
            woods = (TileSetAtlasSource)_donor.GetSource(0);
            if (tileSet.AddSource(woods, TownSource) != TownSource)
                throw new InvalidOperationException($"Farm TileSet refused source {TownSource}.");
        }

        TileSetTools.AddWalkableLayer(tileSet);
        TileSetTools.MakeBlocker(woods, TerrainTiles.Blocker);
        TileSetTools.DeriveWalkable(tileSet);

        return tileSet;
    }
}
