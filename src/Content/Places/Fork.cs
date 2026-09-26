using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The fork: the crossroads west of the town centre — Billie's to the west, town to
/// the east, the farm road north, and a southbound stub that is chained off.
/// CANON (Kevin, 2026-08-26): "South will lead to something else later, but it is
/// currently chained off" — the chain says so without saying what (DEFERRED).
/// No buildings; this frame is all road, so this file is all copy.
/// </summary>
public static class Fork
{
    public const string MapId = MapIds.Fork;

    // [KEVIN] placeholder copy — the directions restate the map graph, and the
    // chain's sign says nothing about what is behind it, which is the point.
    public const string FingerPostText = "North: the farm. East: town. West: the west road.";
    public const string SouthChainSignText = "Road closed.";

    /// <summary>Copy for this place's placed signs, by placement id. Null = an unpromoted
    /// sign (a board still carrying its own map-file text).</summary>
    public static string? SignTextFor(string placementId) => placementId switch
    {
        "FingerPost" => FingerPostText,
        "SouthChainSign" => SouthChainSignText,
        _ => null,
    };
}
