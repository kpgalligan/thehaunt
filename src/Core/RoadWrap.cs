namespace TheHaunt.Core;

/// <summary>
/// The town's primary secret, as geometry (docs/story/README.md): a resident who keeps
/// driving out past the west entry arrives rolling in from the east, and one who leaves
/// east arrives from the west. The two outermost frames wire their outward road mouths
/// through this table so the pair cannot drift apart. Leaving town is never gated — the
/// wrap IS the answer, so both outward exits stay enabled and the player simply finds
/// more town in front of them.
/// </summary>
public static class RoadWrap
{
    /// <summary>The spawn both entry maps give their outward road mouth's arrival.</summary>
    public const string ArrivalSpawn = "wrap";

    /// <summary>Where walking west off the west entry actually lands you.</summary>
    public const string PastTheWestEdgeMap = MapIds.EastEntry;

    /// <summary>Where walking east off the east entry actually lands you.</summary>
    public const string PastTheEastEdgeMap = MapIds.WestEntry;
}
