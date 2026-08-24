namespace TheHaunt.Core;

public interface ISaveMigration
{
    int FromVersion { get; }                     // applies when file version <= FromVersion; see SaveMigrations.Apply
    void Apply(System.Text.Json.Nodes.JsonNode root);
}
