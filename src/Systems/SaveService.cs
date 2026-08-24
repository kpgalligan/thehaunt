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

        foreach (MapState map in data.Maps.Values)
        {
            map.RebuildIndex();
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
