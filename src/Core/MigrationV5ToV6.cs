using System.Text.Json.Nodes;

namespace TheHaunt.Core;

/// <summary>
/// v5 -> v6 (field obstacles): PURELY a version-gate bump. The new state — per-map
/// <c>ObstaclesSeeded</c>, per-object <c>HitsTaken</c>, and the obstacle records
/// themselves — is all additive with safe absent-defaults (false / 0 / generated on
/// next visit), so there is nothing to rewrite. The bump exists so a pre-obstacle
/// build refuses a v6 save (SaveTooNewException) instead of loading it, dropping the
/// fields it does not know, and silently regrowing every tree the player cleared.
///
/// FROZEN — the launch-era shape this migrates between never changes again.
/// </summary>
public sealed class MigrationV5ToV6 : ISaveMigration
{
    public int FromVersion => 5;

    public void Apply(JsonNode root)
    {
        // Nothing to add: absent ObstaclesSeeded reads false and absent HitsTaken
        // reads 0, which are exactly the values a v5 save means.
    }
}
