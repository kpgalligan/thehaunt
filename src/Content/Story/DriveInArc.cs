namespace TheHaunt.Content;

/// <summary>
/// PLANNED — the drive-in restoration arc. Nothing below is built beyond the entry
/// chain (Places/DriveIn) and Shelly's summer presence (Characters/Shelly); no
/// quest, flag, or purchase mechanic exists yet, and a test guards that nothing
/// here leaks into the game until it ships (Content_PlannedStubsAreUnregistered).
///
/// CANON design (Kevin's drive-in doc, 2026-08-29, moved here 2026-09-01):
///
/// Repairing and reopening the drive-in is a major main story goal. It is what
/// unites the town as a unit: the part of the town that doesn't want to fight the
/// haunt will come to know and trust Jane; the part that does will respect Jane and
/// her strength. Reopening the theater restores community and hope.
///
/// Shelly holds no hope of it reopening, but offers to sell the property to Jane.
/// It would free her from the town, but she has no plans to leave — where would she
/// go? She is reluctant to sell, because this is her private space. The price is
/// high: $250k.
///
/// After Jane buys the property, Shelly disappears until Jane gets the projector
/// units working. From that point Shelly becomes an ALLY and helps Jane in various
/// ways with her other tasks (details to be added later).
///
/// To reopen the theater, Jane needs to:
///   - buy it ($250k, from Shelly);
///   - fix the projectors (a multistage task, to be defined later);
///   - repair the audio system for the cars;
///   - repair the community seating;
///   - restore power to the arcade (Shelly then fixes the arcade games herself —
///     she grew up with the place, and is rather excited about it opening again).
///
/// Getting it running requires a more advanced mechanical-repair skill (Kevin,
/// 2026-08-29 — the garage is where that skill is practiced; SkillRules holds the
/// curve). Reopened, the theater provides a regular income split with Shelly, who
/// runs it — freeing Jane to focus on removing the curse.
///
/// Late game, something terrible will probably happen to Shelly and the theater
/// goes dark again — that's when things get very bleak. TBD; do not build toward
/// it without Kevin.
/// </summary>
public static class DriveInArc
{
    /// <summary>Reserved id prefix for the arc's future quests and flags — nothing
    /// registers under it yet (test-guarded).</summary>
    public const string IdPrefix = "drive_in.";
}
