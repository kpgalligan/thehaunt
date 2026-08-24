using System.Text.Json.Nodes;

namespace TheHaunt.Core;

public static class SaveMigrations
{
    public const int CurrentVersion = 1;

    public static IReadOnlyList<ISaveMigration> Chain { get; } = Array.Empty<ISaveMigration>();

    public static JsonNode Apply(JsonNode root) => Apply(root, Chain, CurrentVersion);

    // Test seam: run an arbitrary chain against an arbitrary current version.
    public static JsonNode Apply(JsonNode root, IReadOnlyList<ISaveMigration> chain, int currentVersion)
    {
        int fileVersion = root["SaveVersion"]?.GetValue<int>() ?? 0;
        if (fileVersion > currentVersion)
        {
            throw new SaveTooNewException(fileVersion, currentVersion);
        }
        foreach (var migration in chain.Where(m => m.FromVersion >= fileVersion).OrderBy(m => m.FromVersion))
        {
            migration.Apply(root);
        }
        root["SaveVersion"] = currentVersion;
        return root;
    }
}
