namespace TheHaunt.Core;

/// <summary>
/// Skills v1 (Kevin, 2026-08-30): levels 1-10, every "practice" is 1 XP, every
/// level costs 10 XP — flat curve, explicitly a starting point to be rebalanced
/// later [KEVIN]. XP is the stored truth and keeps accumulating past the cap
/// (rebalancing later must not lose progress); level is always derived. Practice
/// sources: farming = each harvested crop, mechanical repair = each completed
/// garage job (later, repairs out in the world), foraging = each thing gathered,
/// combat = each kill. Foraging and combat have ids and UI only — their mechanics
/// don't exist yet, so nothing grants them. All grants flow through
/// WorldSim.GrantSkillXp (the bus observes outcomes; Core actions stay XP-free,
/// the FirstPlanting pattern).
/// </summary>
public static class SkillRules
{
    public const int MaxLevel = 10;
    public const int XpPerLevel = 10;   // [KEVIN] v1 tuning — "each level requires 10 experience points"

    /// <summary>Total XP banked; absent or negative reads 0.</summary>
    public static long Xp(GameData data, string skillId) =>
        data.Player.SkillXp.TryGetValue(skillId, out long xp) ? Math.Max(0, xp) : 0;

    public static int Level(GameData data, string skillId) => LevelForXp(Xp(data, skillId));

    /// <summary>Level 1 at 0 XP, level 2 at 10, ... level 10 at 90; capped there.</summary>
    public static int LevelForXp(long xp) =>
        (int)Math.Min(MaxLevel, 1 + Math.Max(0, xp) / XpPerLevel);

    /// <summary>Progress inside the current level, for the panel's "4/10". At the
    /// cap the panel shows MAX instead — this value stops meaning anything there.</summary>
    public static long XpIntoLevel(long xp) => Math.Max(0, xp) % XpPerLevel;

    /// <summary>
    /// Banks XP (the one skill mutation) and reports the level edge so the bus can
    /// fire SkillLeveledUp exactly when one is crossed. Non-positive amounts are a
    /// refused no-op (never a drain — XP is monotone like story flags).
    /// </summary>
    public static (int OldLevel, int NewLevel) AddXp(GameData data, string skillId, long amount = 1)
    {
        long before = Xp(data, skillId);
        if (amount <= 0)
        {
            int level = LevelForXp(before);
            return (level, level);
        }
        long after = before + amount;
        data.Player.SkillXp[skillId] = after;
        return (LevelForXp(before), LevelForXp(after));
    }
}
