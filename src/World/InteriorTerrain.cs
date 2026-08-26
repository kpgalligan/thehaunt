using Godot;

namespace TheHaunt.World;

/// <summary>
/// The shared interior TileSet, loaded once from the handoff's resource. Unlike the two
/// exterior sheets this .tres already carries its own "walkable" custom data and box
/// collision — it was authored to match <c>FarmHouseMap.BuildTileSet</c>'s semantics
/// exactly.
///
/// What it cannot carry is a blocker: every one of its 64 cells is drawn, so there is no
/// spare transparent tail to hang a collision-only tile on. A one-tile transparent atlas
/// goes in as a second source instead, and that is what the Obstacles layer paints under
/// a sprite-drawn furniture piece.
///
/// The walkable data is re-derived anyway. The shipped .tres already agrees with its own
/// collision, but "derived" is the project's rule precisely so that a redelivered sheet
/// with a new collision box and a stale true cannot make IsStandable disagree with
/// physics — and it makes the blocker's own data fall out for free.
/// </summary>
public static class InteriorTerrain
{
    public const string TileSetPath = "res://assets/sprites/interior/thehaunt_interior.tres";

    /// <summary>Source id of the interior sheet.</summary>
    public const int TileSource = 0;

    /// <summary>Source id of the transparent blocker atlas; its only tile is (0,0).</summary>
    public const int BlockerSource = 1;

    /// <summary>The blocker's atlas coordinate — paint it with <see cref="BlockerSource"/>.</summary>
    public static readonly Vector2I Blocker = Vector2I.Zero;

    private static TileSet? _cached;

    public static TileSet Get() => _cached ??= Build();

    private static TileSet Build()
    {
        var tileSet = GD.Load<TileSet>(TileSetPath)
            ?? throw new InvalidOperationException($"Interior TileSet missing at '{TileSetPath}'.");

        // The .tres is a process-cached resource and this mutates it, so a second Build
        // meets a set that is already carrying the blocker source. That happens whenever a
        // C# assembly reload clears _cached without clearing Godot's resource cache — an
        // ordinary `dotnet build` with the editor open. Adopt the existing source; only a
        // set that refuses the id outright is a real failure.
        TileSetAtlasSource blockers;
        if (tileSet.HasSource(BlockerSource))
        {
            blockers = (TileSetAtlasSource)tileSet.GetSource(BlockerSource);
        }
        else
        {
            blockers = TileSetTools.TransparentBlockerSource();
            if (tileSet.AddSource(blockers, BlockerSource) != BlockerSource)
                throw new InvalidOperationException($"Interior TileSet refused source {BlockerSource}.");
        }
        TileSetTools.MakeBlocker(blockers, Blocker);
        TileSetTools.DeriveWalkable(tileSet);

        return tileSet;
    }
}
