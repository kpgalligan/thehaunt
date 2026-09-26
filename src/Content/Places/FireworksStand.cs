using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The fireworks stand, west entry (Gloria's — Characters/Gloria).
/// CANON (Kevin, 2026-08-26): useful later in the game as a source of materials for
/// WEAPONS. Kevin's original note had Jane able to buy fireworks and set them off
/// early (they wouldn't help much) — PLANNED, not built: no stand catalog or
/// firework object exists yet, and it needs a focused design before it does
/// (Kevin, 2026-09-01 / D6).
/// </summary>
public static class FireworksStand
{
    public const string MapId = MapIds.WestEntry;   // the stand shares the west entry frame

    // [KEVIN] placeholder copy — canon restatement only, no names.
    public const string RoadSignText = "Fireworks.";

    /// <summary>Copy for this place's placed signs, by placement id. Null = an unpromoted
    /// sign (a board still carrying its own map-file text).</summary>
    public static string? SignTextFor(string placementId) => placementId switch
    {
        "FireworksSign" => RoadSignText,
        _ => null,
    };
}
