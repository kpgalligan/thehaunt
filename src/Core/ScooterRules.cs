namespace TheHaunt.Core;

/// <summary>
/// The scooter's behaviour contract (docs/designs/design_handoff_scooter §Interactions,
/// amended by Kevin 2026-08-27): twice walking speed while mounted, park it anywhere,
/// and it is NEVER lost — no matter where it was left it is back outside the farmhouse
/// after sleeping. It is never stolen; there is no recall button, no minimap pin: until
/// the next morning, walking back to it is the player's problem.
/// </summary>
public static class ScooterRules
{
    /// <summary>Riding speed as a multiple of walking speed (handoff: exactly 2x).</summary>
    public const float SpeedMultiplier = 2f;

    // Home: the farmhouse frontage — on the path apron between the front door (7,7)
    // and the shipping bin (10,8). Scooter_MountAndDismountThroughTheBus pins this
    // against the farm's real geometry (IsStandable on the built map), so a farm
    // relayout fails a test instead of parking the scooter inside a wall.
    public const string HomeMapId = MapIds.Farm;
    public const int HomeTileX = 9;
    public const int HomeTileY = 8;

    /// <summary>Facing left — the parked sheet's side view, the hero read at 1x.</summary>
    public const int HomeFacing = 1;

    /// <summary>
    /// Parked-sheet column for a rider facing (0=down 1=left 2=right 3=up): profile
    /// dismounts keep the side view (col 0), a downward dismount shows the front
    /// (col 1), an upward one the three-quarter (col 2) — there is no back view.
    /// </summary>
    public static int ParkedColumn(int facing) => facing switch
    {
        0 => 1,
        3 => 2,
        _ => 0,
    };

    /// <summary>The side view is authored facing RIGHT (like the riding sheet's
    /// profile row); mirror it for a leftward dismount.</summary>
    public static bool ParkedFlipH(int facing) => facing == 1;
}
