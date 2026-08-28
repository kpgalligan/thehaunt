using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using TheHaunt.Core;

namespace TheHaunt.Systems;

public enum LoadResult { Ok, NoFile, Corrupt, TooNew }

/// <summary>
/// Autoload that owns the current save-data graph and the save/load pipeline.
/// Writes are atomic (tmp + rename); loads never clobber <see cref="Current"/> on failure,
/// and unreadable files are quarantined so a later Save can never destroy them.
/// </summary>
public partial class SaveService : Node
{
    public static SaveService Instance { get; private set; } = null!;

    /// <summary>Slot used when none is passed. Tests point this at a test slot.</summary>
    public static string DefaultSlot { get; set; } = "save1";

    public static string SaveDirectory => ProjectSettings.GlobalizePath("user://saves/");

    /// <summary>Never null; starts as a fresh <see cref="GameData.NewGame"/>.</summary>
    public GameData Current { get; private set; } = GameData.NewGame();

    public event Action? BeforeSave;

    /// <summary>Fires after Load, DeserializeFrom, AND NewGame — the UI refresh hook.</summary>
    public event Action? AfterLoad;

    private readonly List<IPersistentSystem> _systems = new();

    public override void _EnterTree()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public void Register(IPersistentSystem system)
    {
        if (!_systems.Contains(system))
        {
            _systems.Add(system);
        }
    }

    public void Unregister(IPersistentSystem system)
    {
        _systems.Remove(system);
    }

    public void NewGame()
    {
        Current = GameData.NewGame();
        Clock.Instance.SetTime(new GameTime(0));
        foreach (IPersistentSystem system in _systems)
        {
            system.ReadState(Current);
        }
        AfterLoad?.Invoke();
    }

    public bool Save(string? slot = null)
    {
        string resolvedSlot = slot ?? DefaultSlot;
        try
        {
            foreach (IPersistentSystem system in _systems)
            {
                system.WriteState(Current);
            }
            Current.TotalMinutes = Clock.Instance.Now.TotalMinutes;
            BeforeSave?.Invoke();

            string json = JsonSerializer.Serialize(Current, SaveJsonContext.Default.GameData);
            Directory.CreateDirectory(SaveDirectory);
            string path = SlotPath(resolvedSlot);
            string tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
            return true;
        }
        catch (Exception e)
        {
            GD.PushError($"Save failed for slot '{resolvedSlot}': {e}");
            return false;
        }
    }

    public LoadResult Load(string? slot = null)
    {
        string resolvedSlot = slot ?? DefaultSlot;
        string path = SlotPath(resolvedSlot);
        if (!File.Exists(path))
        {
            return LoadResult.NoFile;
        }
        try
        {
            DeserializeFrom(File.ReadAllText(path));
            return LoadResult.Ok;
        }
        catch (SaveTooNewException e)
        {
            GD.PushError($"Save '{resolvedSlot}' is from a newer build (v{e.FileVersion} > v{e.CurrentVersion}).");
            Quarantine(path, ".toonew");
            return LoadResult.TooNew;
        }
        catch (Exception e)
        {
            GD.PushError($"Load failed for slot '{resolvedSlot}': {e}");
            Quarantine(path, ".bad");
            return LoadResult.Corrupt;
        }
    }

    // Rename an unreadable save aside so a later Save to the slot can never destroy it.
    private static void Quarantine(string path, string suffix)
    {
        try
        {
            string target = path + suffix;
            for (int i = 2; File.Exists(target); i++)
            {
                target = path + suffix + i;
            }
            File.Move(path, target);
            GD.PushWarning($"Unreadable save preserved as '{target}'.");
        }
        catch (Exception e)
        {
            GD.PushError($"Failed to quarantine unreadable save '{path}': {e}");
        }
    }

    public bool SaveFileExists(string? slot = null)
    {
        return File.Exists(SlotPath(slot ?? DefaultSlot));
    }

    /// <summary>Serializes <see cref="Current"/> as-is — no WriteState pass, no BeforeSave.</summary>
    public string SerializeToString()
    {
        return JsonSerializer.Serialize(Current, SaveJsonContext.Default.GameData);
    }

    /// <summary>
    /// Full load path minus file IO. Throws on malformed or too-new JSON; on any failure
    /// before the swap, <see cref="Current"/> is left untouched.
    /// </summary>
    public void DeserializeFrom(string json)
    {
        JsonNode root = JsonNode.Parse(json)
            ?? throw new JsonException("Save data parsed to a null JSON node.");
        root = SaveMigrations.Apply(root);
        GameData data = root.Deserialize(SaveJsonContext.Default.GameData)
            ?? throw new JsonException("Save data deserialized to null.");

        // Semantic validation: parseable-but-invalid values must fail BEFORE the swap,
        // not blow up in consumers after the load half-applied.
        data.Player ??= new PlayerData();
        data.Maps ??= new Dictionary<string, MapState>();
        if (data.TotalMinutes < 0)
        {
            throw new JsonException($"Save data invalid: negative TotalMinutes ({data.TotalMinutes}).");
        }
        if (data.Player.Money < 0)
        {
            throw new JsonException($"Save data invalid: negative Money ({data.Player.Money}).");
        }
        data.Player.Inventory ??= new InventoryData();
        data.Player.Inventory.Normalize();
        data.ShippingBin ??= new();
        // Bin repair mirrors Inventory.Normalize: drop degenerate entries (null element,
        // null/empty id, non-positive count) that would NRE or mint negative money in the
        // overnight sale. Unknown-but-well-formed ids are deliberately KEPT.
        data.ShippingBin.RemoveAll(s => s is null || string.IsNullOrEmpty(s.ItemId) || s.Count <= 0);
        // Flag repair mirrors the bin: drop the one degenerate key, clamp bad stamps.
        // Unknown-but-well-formed flag ids are deliberately KEPT (preserve-unknown rule).
        data.StoryFlags ??= new();
        data.StoryFlags.Remove("");
        foreach (string key in data.StoryFlags.Keys.ToList())
        {
            if (data.StoryFlags[key] < 0)
            {
                data.StoryFlags[key] = 0;
            }
        }
        // A future-dated stamp on a KNOWN intro flag wedges the day-comparison rules
        // (a first_planting stamped past today means the road never clears, and
        // only-if-absent flags cannot be restamped by replaying). Clamp them to the
        // save's own day. Unknown flags keep their stamps verbatim — their semantics
        // are not ours to repair.
        long saveDay = new GameTime(data.TotalMinutes).DayIndex;
        foreach (string key in new[]
        {
            StoryKeys.FirstPlanting, StoryKeys.RoadCleared,
            StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
        })
        {
            if (data.StoryFlags.TryGetValue(key, out long stamp) && stamp > saveDay)
            {
                data.StoryFlags[key] = saveDay;
            }
        }
        // Storage repair mirrors the flag repair: drop the one degenerate key, revive
        // null values, then normalize each entry. Known storage ids pad to capacity;
        // unknown KEYS round-trip un-padded (their capacity is not ours to invent)
        // with only degenerate-entry nulling. Unknown item ids inside are KEPT.
        data.Storages ??= new();
        data.Storages.Remove("");
        foreach (string key in data.Storages.Keys.ToList())
        {
            var storage = data.Storages[key] ??= new StorageData();
            storage.Normalize(StorageIds.CapacityOf(key));
        }
        // Scooter repair: a pre-scooter save (or a nulled record) parks it at home —
        // that is also how existing saves acquire it. A bad facing clamps; an
        // unknown-but-well-formed MapId is KEPT (preserve-unknown rule — the view
        // just won't spawn until the overnight reset brings it home).
        data.Scooter ??= ScooterData.AtHome();
        if (string.IsNullOrEmpty(data.Scooter.MapId)
            || data.Scooter.TileX < 0 || data.Scooter.TileX >= 512
            || data.Scooter.TileY < 0 || data.Scooter.TileY >= 512)
        {
            // No map is anywhere near 512 tiles; out-of-range coords are a hostile
            // writer, and a view parked off-map is a scooter lost for a day.
            data.Scooter = ScooterData.AtHome();
        }
        data.Scooter.Facing = Math.Clamp(data.Scooter.Facing, 0, 3);
        // Never ridden — or parked — indoors (scooter handoff): the rule is enforced
        // at the travel boundary in play, so an interior state here is a hand-edited
        // or drifted save. Re-park at home rather than load the impossible state.
        // Unknown map ids pass (IsInterior is false for them — preserve-unknown).
        if ((data.Scooter.Mounted && MapIds.IsInterior(data.Player.MapId))
            || (!data.Scooter.Mounted && MapIds.IsInterior(data.Scooter.MapId)))
        {
            data.Scooter = ScooterData.AtHome();
        }
        data.Player.MaxStamina = Math.Max(1, data.Player.MaxStamina);
        data.Player.Stamina = Math.Clamp(data.Player.Stamina, 0, data.Player.MaxStamina);

        foreach (MapState map in data.Maps.Values)
        {
            map.RebuildIndex();
            // Objects gets the bin's repair: degenerate records (null element, no id)
            // are dropped and damage clamps non-negative; unknown ids are KEPT. The
            // obstacle path dereferences this list on every boot, so a hand-edited
            // null here must die on load repair, not in EnsureObstacles.
            map.Objects ??= new List<PlacedObjectRecord>();
            map.Objects.RemoveAll(obj => obj is null || string.IsNullOrEmpty(obj.ObjectId));
            foreach (PlacedObjectRecord obj in map.Objects)
            {
                obj.HitsTaken = Math.Max(0, obj.HitsTaken);
            }
        }

        Current = data;
        Clock.Instance.SetTime(new GameTime(Current.TotalMinutes));
        foreach (IPersistentSystem system in _systems)
        {
            system.ReadState(Current);
        }
        AfterLoad?.Invoke();
    }

    private static string SlotPath(string slot) => Path.Combine(SaveDirectory, slot + ".json");
}
