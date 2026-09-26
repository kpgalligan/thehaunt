using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The east entry: police station and hardware store north of the road, Sam's salon
/// across it (Characters/Sam). The east mouth is the other road out of town — for a
/// resident it wraps to the west entry (RoadWrap).
/// PLANNED — THE POLICE (Kevin, 2026-08-26): "we'll detail later, but they'll have
/// an important place in the story." Cast-empty by design; no sprites exist.
/// CANON — THE HARDWARE STORE: starts closed; the owner is in the hospital.
/// PROPOSED [KEVIN]: the sign says only that it is closed — nobody mentions the
/// hospital stay, and the dark band sign (nobody paying its bill) is the only
/// other tell.
/// </summary>
public static class EastEntry
{
    public const string MapId = MapIds.EastEntry;

    // [KEVIN] placeholder copy — canon restatement only, no names. The hardware
    // sign says only that it is closed; the hospital stay goes unmentioned.
    public const string PoliceSignText = "Police.";
    public const string HardwareSignText = "Hardware. Closed until further notice.";
    public const string SalonSignText = "Salon.";

    /// <summary>Copy for this place's placed signs, by placement id. Null = an unpromoted
    /// sign (a board still carrying its own map-file text).</summary>
    public static string? SignTextFor(string placementId) => placementId switch
    {
        "PoliceSign" => PoliceSignText,
        "HardwareSign" => HardwareSignText,
        "SalonSign" => SalonSignText,
        _ => null,
    };
}
