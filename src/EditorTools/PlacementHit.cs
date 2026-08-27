using Godot;
using TheHaunt.World;

namespace TheHaunt.EditorTools;

/// <summary>
/// Which record a click on a tile grabs. The whole of "dragging is not blind" comes down
/// to this being the one you meant.
///
/// Footprints overlap constantly on a map with trees on it — nine trees, each four tiles
/// tall, over pasture strewn with boulders — so a hit is RANKED rather than found:
///
///   1. A hit on a record's OWN cell beats a hit anywhere else in its footprint. That
///      cell is what "x, y" means in the file, and it is drawn as the filled square in
///      the overlay, so it is also the thing a person is aiming at.
///   2. Then the tighter footprint, so a boulder under a canopy is reachable at all
///      rather than permanently shadowed by the twelve cells the tree claims.
///   3. Then the later record, which is the one drawn on top.
///
/// Editor-only, and pure — no node, no editor API — which is what lets the headless suite
/// cover it. Returns an INDEX into <see cref="MapRecipe.Placements"/> and not a record,
/// because that is the identity the mapper's selection is held as: insertion order
/// survives a drag (which mutates in place) where an object reference does not survive a
/// re-parse.
/// </summary>
public static class PlacementHit
{
    // Tiers, spaced so no amount of the term below can climb into the one above: a recipe
    // is dozens of records and a footprint is a handful of cells, both far under 1 << 12.
    private const int AnchorTier = 1 << 24;
    private const int AreaTier = 1 << 12;

    /// <summary>The placement a click on <paramref name="tile"/> grabs, or -1 for empty ground.</summary>
    public static int At(MapRecipe recipe, Vector2I tile)
    {
        IReadOnlyList<MapPlacement> placements = recipe.Placements;
        int best = -1;
        int bestScore = int.MinValue;
        for (int i = 0; i < placements.Count; i++)
        {
            MapPlacement placement = placements[i];
            if (!PlacementFootprint.Of(placement).HasPoint(tile))
            {
                continue;
            }
            int score = (placement.Cell == tile ? AnchorTier : 0)
                - PlacementFootprint.Area(placement) * AreaTier + i;
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }
}
