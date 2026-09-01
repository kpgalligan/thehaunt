namespace TheHaunt.Core;

/// <summary>
/// One-shot random field-obstacle generation ("randomly generated, but only in
/// certain areas of the map" — Kevin, 2026-08-28). Runs ONCE per map per save
/// (WorldSim.EnsureObstacles gates on <see cref="MapState.ObstaclesSeeded"/>);
/// the results are save state from then on, so the seed never needs storing.
///
/// The CANDIDATE cells are the map view's contribution (MapRoot.ObstacleCandidates):
/// only the map knows its own surfaces and reservations. This stays pure and
/// deterministic — same candidates, same map state, same seed, same layout — with
/// its own tiny PRNG so no runtime's Random rewrite can silently reshuffle a test.
/// </summary>
public static class ObstacleGen
{
    // Counts are targets, not guarantees: spacing and occupancy can leave fewer.
    public const int TreeTarget = 12;
    public const int StumpTarget = 3;
    public const int RockTarget = 10;

    /// <summary>Trees keep their trunks this far apart (Chebyshev) so canopies read.</summary>
    public const int TreeSpacing = 3;

    public static List<PlacedObjectRecord> Generate(
        IReadOnlyList<(int X, int Y)> candidates, MapState map, int seed)
    {
        // Open cells only: an old save's worked plots and anything already standing
        // (an object record of ANY kind, known here or not) are never built over.
        var open = new List<(int X, int Y)>();
        foreach ((int X, int Y) cell in candidates)
        {
            if (map.GetTile(cell.X, cell.Y) is null && map.GetObject(cell.X, cell.Y) is null)
            {
                open.Add(cell);
            }
        }
        Shuffle(open, seed);

        var placed = new List<PlacedObjectRecord>();
        Place(open, placed, ObstacleDefs.Tree, TreeTarget);
        Place(open, placed, ObstacleDefs.Stump, StumpTarget);
        Place(open, placed, ObstacleDefs.Rock, RockTarget);
        return placed;
    }

    private static void Place(
        List<(int X, int Y)> open, List<PlacedObjectRecord> placed, string objectId, int target)
    {
        int count = 0;
        for (int i = 0; i < open.Count && count < target; i++)
        {
            (int x, int y) = open[i];
            if (!Fits(placed, objectId, x, y))
            {
                continue;
            }
            placed.Add(new PlacedObjectRecord { X = x, Y = y, ObjectId = objectId });
            open.RemoveAt(i);
            i--;
            count++;
        }
    }

    // No two obstacles adjacent (Chebyshev > 1): every generated obstacle is a lone
    // cell with open ground on all sides. That keeps the field walkable only together
    // with the candidate contract — the map must not offer one-tile corridors (the
    // farm excludes the strip around its pen for exactly this reason).
    private static bool Fits(List<PlacedObjectRecord> placed, string objectId, int x, int y)
    {
        foreach (PlacedObjectRecord other in placed)
        {
            int distance = Math.Max(Math.Abs(other.X - x), Math.Abs(other.Y - y));
            if (distance <= 1)
            {
                return false;
            }
            if (objectId == ObstacleDefs.Tree && other.ObjectId == ObstacleDefs.Tree
                && distance < TreeSpacing)
            {
                return false;
            }
            // Canopies stay hollow, in both directions (trees place first): a solid
            // cell hidden under foliage is an invisible wall — the exact thing the
            // old hand layout moved its boulders to avoid.
            if (other.ObjectId == ObstacleDefs.Tree && UnderCanopy(other.X, other.Y, x, y))
            {
                return false;
            }
            if (objectId == ObstacleDefs.Tree && UnderCanopy(x, y, other.X, other.Y))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The 3x4 footprint a tree draws over: trunk row and three rows up, one column each side.</summary>
    private static bool UnderCanopy(int trunkX, int trunkY, int x, int y) =>
        Math.Abs(x - trunkX) <= 1 && y >= trunkY - 3 && y <= trunkY;

    // Fisher-Yates over an xorshift32 stream — self-owned so the sequence can never
    // drift under a .NET Random rewrite. Seed 0 would freeze xorshift; fold it away.
    private static void Shuffle(List<(int X, int Y)> cells, int seed)
    {
        uint state = seed == 0 ? 2463534242u : (uint)seed;
        for (int i = cells.Count - 1; i > 0; i--)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            int j = (int)(state % (uint)(i + 1));
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
    }
}
