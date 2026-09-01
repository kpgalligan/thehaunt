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

    // The save's one deterministic RNG seed (the garage's hourly customer roll —
    // GarageOpsRules.CustomerRoll hashes it with day + hour). Rolled once by
    // SaveService.NewGame; absent in pre-v7 saves -> 0, which is a valid seed
    // (every migrated save shares one arrival schedule — cosmetically harmless).
    public int Seed { get; set; }

    // Cars in the owned garage, at most GarageOpsRules.MaxCars. CONTENT-bearing
    // save state, not a derivation: arrival is a die roll, work is banked presses.
    public List<GarageJobRecord> GarageJobs { get; set; } = new();

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

    // Defaults: time 0, player MapId "test_farm", HasPosition false, full
    // stamina, empty hands — the starter kit waits in the barn chest. Money
    // starts at the TEMPORARY DevScaffold floor (was 500g) so day 1 can buy the
    // garage; the scaffold's own doc says how to unwind it. Seed 0 (tests stay
    // deterministic); the real game passes GD.Randi via SaveService.NewGame.
    public static GameData NewGame(int seed = 0)
    {
        var data = new GameData();
        data.Seed = seed;
        data.Player.Money = DevScaffold.DailyMoneyFloor;
        data.Player.Stamina = 100;
        data.Player.MaxStamina = 100;
        StarterKit.Apply(data);
        return data;
    }
}
