namespace TheHaunt.Core;

public sealed class GameData
{
    public int SaveVersion { get; set; } = SaveMigrations.CurrentVersion;
    public long TotalMinutes { get; set; }
    public PlayerData Player { get; set; } = new();
    public Dictionary<string, MapState> Maps { get; set; } = new();

    public MapState GetMap(string mapId)
    {
        if (!Maps.TryGetValue(mapId, out var map))
        {
            map = new MapState();
            Maps[mapId] = map;
        }
        return map;
    }

    // Defaults: time 0, player MapId "test_farm", HasPosition false.
    public static GameData NewGame() => new();
}
