using System.Text.Json.Nodes;

namespace TheHaunt.Core;

public static class SaveMigrations
{
    public const int CurrentVersion = 4;

    public static IReadOnlyList<ISaveMigration> Chain { get; } = new ISaveMigration[]
    {
        new MigrationV1ToV2(),
        new MigrationV2ToV3(),
        new MigrationV3ToV4(),
    };

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
