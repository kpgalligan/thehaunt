namespace TheHaunt.Core;

/// <summary>
/// Where the one scooter is (docs/designs/design_handoff_scooter). Exactly one
/// exists: it is either parked on a tile of some map, or under the player
/// (<see cref="Mounted"/>) — never both, never neither. Absent from an older save,
/// the property default parks it at home, which is also how every existing save
/// acquires it (Kevin: the player has it from the start for now — the mid-game
/// acquisition seam is deliberately unbuilt, like the barn's states).
/// </summary>
public sealed class ScooterData
{
    /// <summary>Map the parked scooter is on. Unknown ids round-trip verbatim.</summary>
    public string MapId { get; set; } = ScooterRules.HomeMapId;

    public int TileX { get; set; } = ScooterRules.HomeTileX;

    public int TileY { get; set; } = ScooterRules.HomeTileY;

    /// <summary>
    /// The PLAYER's facing at dismount (0=down 1=left 2=right 3=up) — the parked
    /// sheet cell derives from it (<see cref="ScooterRules.ParkedColumn"/>), so the
    /// world remembers which way the rider stepped off.
    /// </summary>
    public int Facing { get; set; } = ScooterRules.HomeFacing;

    /// <summary>True while the player rides; the parked world object does not exist.</summary>
    public bool Mounted { get; set; }

    public static ScooterData AtHome() => new();
}
