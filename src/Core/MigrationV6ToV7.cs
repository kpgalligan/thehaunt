using System.Text.Json.Nodes;

namespace TheHaunt.Core;

/// <summary>
/// v6 -> v7 (skills + garage operation): PURELY a version-gate bump, the
/// MigrationV5ToV6 shape. The new state — <c>Seed</c>, <c>Player.SkillXp</c>,
/// and <c>GarageJobs</c> — is all additive with safe absent-defaults (0 / empty /
/// empty), so there is nothing to rewrite. The bump exists so a pre-skills build
/// refuses a v7 save (SaveTooNewException) instead of loading it, dropping the
/// fields it does not know, and silently erasing banked XP and cars in the shop.
///
/// FROZEN — the launch-era shape this migrates between never changes again.
/// </summary>
public sealed class MigrationV6ToV7 : ISaveMigration
{
    public int FromVersion => 6;

    public void Apply(JsonNode root)
    {
        // Nothing to add: absent Seed reads 0, absent SkillXp reads empty, absent
        // GarageJobs reads empty — exactly the values a v6 save means.
    }
}
