namespace TheHaunt.Core;

/// <summary>
/// The four skills (Kevin, 2026-08-30): farming, mechanical repair, foraging,
/// combat. String ids because they key <see cref="PlayerData.SkillXp"/> in the
/// save — same contract as story flags: unknown ids from saves round-trip
/// untouched, and renaming one ships as a migration.
/// </summary>
public static class SkillIds
{
    public const string Farming = "farming";
    public const string MechanicalRepair = "mechanical_repair";
    public const string Foraging = "foraging";
    public const string Combat = "combat";

    /// <summary>Canonical order for UI listings (the skills panel).</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Farming, MechanicalRepair, Foraging, Combat,
    };

    // Unknown ids echo back rather than throw — defensive: display code must never
    // crash on an id that rode in from a future save. (The panel itself lists only
    // SkillIds.All today; unknown ids are preserved in the save, not shown.)
    public static string DisplayName(string id) => id switch
    {
        Farming => "Farming",
        MechanicalRepair => "Mechanical Repair",
        Foraging => "Foraging",
        Combat => "Combat",
        _ => id,
    };
}
