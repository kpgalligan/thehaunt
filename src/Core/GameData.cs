namespace TheHaunt.Core;

public sealed class GameData
{
    public int SaveVersion { get; set; } = SaveMigrations.CurrentVersion;
    public long TotalMinutes { get; set; }
    public PlayerData Player { get; set; } = new();
    public Dictionary<string, MapState> Maps { get; set; } = new();
    public List<ItemStackRecord> ShippingBin { get; set; } = new();
    public Dictionary<string, long> StoryFlags { get; set; } = new();   // flag id -> DayIndex stamped
    public Dictionary<string, StorageData> Storages { get; set; } = new();   // storage id -> slots
    public ScooterData Scooter { get; set; } = ScooterData.AtHome();   // absent in pre-scooter saves -> home

    public MapState GetMap(string mapId)
    {
        if (!Maps.TryGetValue(mapId, out var map))
        {
            map = new MapState();
            Maps[mapId] = map;
        }
        return map;
    }

    // Lazy-create mirroring GetMap; new storages are normalized to their known
    // capacity (unknown ids stay un-padded). NewGame stocks only the barn chest
    // (StarterKit) — every other chest materializes on first open.
    public StorageData GetStorage(string id)
    {
        if (!Storages.TryGetValue(id, out var storage))
        {
            storage = new StorageData();
            storage.Normalize(StorageIds.CapacityOf(id));
            Storages[id] = storage;
        }
        return storage;
    }

    public bool HasFlag(string id) => StoryFlags.ContainsKey(id);

    public long FlagDay(string id) => StoryFlags.TryGetValue(id, out var d) ? d : -1;

    // Only-if-absent; true iff newly set. Flags are monotone — no unset API,
    // absence = false, stamp = day-index (never a bool). Unknown keys from saves
    // round-trip untouched.
    public bool TrySetFlag(string id, long day)
    {
        if (StoryFlags.ContainsKey(id))
        {
            return false;
        }
        StoryFlags[id] = day;
        return true;
    }

    // Defaults: time 0, player MapId "test_farm", HasPosition false,
    // 500g, full stamina, empty hands — the starter kit waits in the barn chest.
    public static GameData NewGame()
    {
        var data = new GameData();
        data.Player.Money = 500;
        data.Player.Stamina = 100;
        data.Player.MaxStamina = 100;
        StarterKit.Apply(data);
        return data;
    }
}
