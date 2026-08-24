# The Haunt — Foundation Spec v1 (authoritative)

This is the implementation contract for the foundation. Multiple implementers build modules
in parallel against this document. **Public signatures listed here are EXACT — implement them
verbatim.** Private/internal helpers are free. If something here seems wrong or incomplete,
implement the closest-to-spec interpretation and report the deviation — do not silently invent
a different contract.

## 0. Global rules

- Godot 4.7.2 .NET, C# only, net8.0, RootNamespace `TheHaunt`. Nullable enabled, ImplicitUsings enabled.
- Style: file-scoped namespaces, 4-space indent, one public type per file (a small support type may share a file with its owner).
- Namespaces mirror folders: `src/Core` → `TheHaunt.Core`, `src/Systems` → `TheHaunt.Systems`, `src/World` → `TheHaunt.World`, `src/Player` → `TheHaunt.Player`, `src/UI` → `TheHaunt.UI`, `src/Tests` → `TheHaunt.Tests`.
- `src/Core` is PURE C#: `using Godot` is forbidden there (a reflection test enforces no Godot types in the save DTO graph).
- C# events do NOT auto-disconnect when a node is freed (Godot signals do). Hard rule: subscribe in `_Ready`/`_EnterTree`, ALWAYS unsubscribe in `_ExitTree`.
- Standing rule: entities never subscribe to time events; systems do. `MinuteTicked` is for display (HUD) only; `TenMinuteTicked` is the canonical gameplay/sim hook.
- Tiles are 16 px. Tile `(x, y)` world center = `(x*16+8, y*16+8)`.
- Physics layers: layer 1 (bit value 1) = world/blocking collision; layer 2 (bit value 2) = interactable Area2Ds.
- Input actions (defined in project.godot by the integrator): `move_up`, `move_down`, `move_left`, `move_right`, `interact` (E / Space), `pause` (Esc).
- Facing encoding everywhere: `0=down, 1=left, 2=right, 3=up`. Direction vectors: down `(0,1)`, left `(-1,0)`, right `(1,0)`, up `(0,-1)`.
- Autoloads (registered by integrator, in this order): `GameState`, `Clock`, `SaveService`. Each sets a static `Instance` property and `ProcessMode = ProcessModeEnum.Always` in `_EnterTree`.
- Placeholder art is procedural: `Image.CreateEmpty(w, h, false, Image.Format.Rgba8)` + `SetPixel` + `ImageTexture.CreateFromImage`. Flat colors, no anti-aliasing. Any randomness must be deterministic (fixed seed or coordinate hash) so tests are stable.
- Do not add cross-module dependencies beyond what this spec lists.

## 1. src/Core — pure C# (files: Season.cs, GameTime.cs, ClockModel.cs, GameData.cs, PlayerData.cs, MapState.cs, TileRecord.cs, PlacedObjectRecord.cs, IPersistentSystem.cs, ISaveMigration.cs, SaveTooNewException.cs, SaveMigrations.cs, SaveJsonContext.cs)

### Season.cs
```csharp
namespace TheHaunt.Core;
public enum Season { Spring = 0, Summer = 1, Fall = 2, Winter = 3 }
```

### GameTime.cs
```csharp
namespace TheHaunt.Core;

public readonly record struct GameTime(long TotalMinutes)
{
    public const int MinutesPerDay = 1200;   // a day runs 6:00 -> 26:00 (2 AM), monotonic past midnight
    public const int DayStartHour = 6;
    public const int DaysPerSeason = 28;
    public const int SeasonsPerYear = 4;
    public const int DaysPerYear = 112;

    public long DayIndex { get; }        // TotalMinutes / MinutesPerDay
    public int MinuteOfDay { get; }      // (int)(TotalMinutes % MinutesPerDay), 0..1199
    public int AbsoluteHour { get; }     // DayStartHour + MinuteOfDay / 60 -> 6..25 (24 = midnight, 25 = 1 AM)
    public int Minute { get; }           // MinuteOfDay % 60
    public Season Season { get; }        // (Season)((DayIndex / DaysPerSeason) % SeasonsPerYear)
    public int DayOfSeason { get; }      // (int)(DayIndex % DaysPerSeason) + 1  -> 1..28
    public int Year { get; }             // (int)(DayIndex / DaysPerYear) + 1    -> 1-based
    public int WeekdayIndex { get; }     // (int)(DayIndex % 7); 0 = Monday (day 1 of spring, year 1 is a Monday)

    public GameTime AddMinutes(long minutes);            // new GameTime(TotalMinutes + minutes)
    public static GameTime StartOfDay(long dayIndex);    // new GameTime(dayIndex * MinutesPerDay)
    public string ToClockString();
    public string ToDateString();
}
```
- `ToClockString()`: 12-hour with AM/PM. Let `h24 = AbsoluteHour % 24`; suffix `"AM"` if `h24 < 12` else `"PM"`; `h12 = h24 % 12`, and `0 -> 12`. Format `$"{h12}:{Minute:00} {suffix}"`. Examples: `t=0` → `"6:00 AM"`, `+360` → `"12:00 PM"`, `+1080` → `"12:00 AM"`, `+1140` → `"1:00 AM"`, `+1199` → `"1:59 AM"`.
- `ToDateString()`: `$"{Weekday} {Season} {DayOfSeason}, Year {Year}"` using weekday names `"Mon.","Tue.","Wed.","Thu.","Fri.","Sat.","Sun."` and season names `"Spring","Summer","Fall","Winter"`. Example: `t=0` → `"Mon. Spring 1, Year 1"`.

### ClockModel.cs
```csharp
namespace TheHaunt.Core;

public sealed class ClockModel
{
    public GameTime Now { get; private set; }               // starts at new GameTime(0)
    public double SecondsPerGameMinute { get; set; } = 0.7; // Stardew pace
    public double TimeScale { get; set; } = 1.0;
    public int MaxMinutesPerFrame { get; set; } = 5;
    public bool AtEndOfDay { get; }                          // Now.MinuteOfDay >= GameTime.MinutesPerDay - 1

    public event Action<GameTime>? MinuteTicked;      // display-only consumers (HUD)
    public event Action<GameTime>? TenMinuteTicked;   // canonical sim hook
    public event Action<GameTime>? HourTicked;
    public event Action<GameTime>? DayEnded;          // payload: time before the day advance
    public event Action<GameTime>? DayStarted;        // payload: new day's 6:00 time

    public void Accumulate(double deltaSeconds);
    public void AdvanceMinutes(int minutes);          // deterministic test/dev seam, no per-frame cap
    public void AdvanceToDayStart();
    public void SetTime(GameTime time);               // load path: sets Now, clears accumulator, fires NOTHING
}
```
Tick semantics (exact):
- Private `StepOneMinute()`: `Now = Now.AddMinutes(1)`; fire `MinuteTicked(Now)`; if `Now.MinuteOfDay % 10 == 0` fire `TenMinuteTicked(Now)`; if `Now.MinuteOfDay % 60 == 0` fire `HourTicked(Now)`. Events fire sequentially, in order, one minute at a time — never batched.
- `Accumulate(delta)`: `_acc += delta * TimeScale`; then loop: while `_acc >= SecondsPerGameMinute` and steps this call `< MaxMinutesPerFrame`: if `AtEndOfDay` then `_acc = 0` and return; else `_acc -= SecondsPerGameMinute; StepOneMinute();`. After the loop, if `_acc > SecondsPerGameMinute`, clamp `_acc = SecondsPerGameMinute` (discard runaway backlog).
- `AdvanceMinutes(n)`: repeat n times: if `AtEndOfDay` stop; `StepOneMinute()`.
- Ticking NEVER crosses a day boundary: the clock clamps at MinuteOfDay 1199 (1:59 AM) and stops. Day advance happens ONLY via `AdvanceToDayStart()`: fire `DayEnded(Now)`; `Now = GameTime.StartOfDay(Now.DayIndex + 1)`; clear accumulator; fire `DayStarted(Now)`. It does NOT fire minute/ten-minute/hour ticks.

### GameData.cs
```csharp
namespace TheHaunt.Core;

public sealed class GameData
{
    public int SaveVersion { get; set; } = SaveMigrations.CurrentVersion;
    public long TotalMinutes { get; set; }
    public PlayerData Player { get; set; } = new();
    public Dictionary<string, MapState> Maps { get; set; } = new();

    public MapState GetMap(string mapId);      // lazy-create + store
    public static GameData NewGame();          // defaults: time 0, player MapId "test_farm", HasPosition false
}
```

### PlayerData.cs
```csharp
namespace TheHaunt.Core;
public sealed class PlayerData
{
    public string MapId { get; set; } = "test_farm";
    public float X { get; set; }
    public float Y { get; set; }
    public int Facing { get; set; }              // 0=down 1=left 2=right 3=up
    public bool HasPosition { get; set; }        // false until first WriteState; NaN is not JSON-safe
}
```

### MapState.cs
```csharp
namespace TheHaunt.Core;

public sealed class MapState
{
    public List<TileRecord> Tiles { get; set; } = new();
    public List<PlacedObjectRecord> Objects { get; set; } = new();

    // Runtime index: packed coord -> position in Tiles. [JsonIgnore]d, rebuilt on load.
    // Packing: static long Pack(int x, int y) => ((long)y << 32) | (uint)x;
    public TileRecord? GetTile(int x, int y);
    public void SetTile(TileRecord record);      // upsert by (X, Y), maintains index
    public bool RemoveTile(int x, int y);        // swap-remove, fixes index; true if removed
    public void RebuildIndex();                  // call after deserialization
}
```

### TileRecord.cs / PlacedObjectRecord.cs
```csharp
namespace TheHaunt.Core;
public sealed class TileRecord
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Kind { get; set; } = "";       // e.g. "tilled" later; sparse deltas only
    public string? CropId { get; set; }
    public int GrowthDay { get; set; }
    public long LastWateredDay { get; set; } = -1;  // day-index, -1 = never. NOT a bool — survives skipped days.
}
public sealed class PlacedObjectRecord
{
    public int X { get; set; }
    public int Y { get; set; }
    public string ObjectId { get; set; } = "";
}
```

### IPersistentSystem.cs
```csharp
namespace TheHaunt.Core;
// Node-owned volatile state only (player position/facing is essentially the whole list).
// If this registry grows past ~the player, state is leaking into the scene tree.
public interface IPersistentSystem
{
    void WriteState(GameData data);
    void ReadState(GameData data);
}
```

### ISaveMigration.cs / SaveTooNewException.cs / SaveMigrations.cs
```csharp
namespace TheHaunt.Core;
public interface ISaveMigration
{
    int FromVersion { get; }                     // applies when file version <= FromVersion... see Apply
    void Apply(System.Text.Json.Nodes.JsonNode root);
}
public sealed class SaveTooNewException : Exception
{
    public int FileVersion { get; }
    public int CurrentVersion { get; }
    public SaveTooNewException(int fileVersion, int currentVersion);  // message names both
}
public static class SaveMigrations
{
    public const int CurrentVersion = 1;
    public static IReadOnlyList<ISaveMigration> Chain { get; }   // empty at v1
    public static JsonNode Apply(JsonNode root);                 // uses Chain + CurrentVersion
    public static JsonNode Apply(JsonNode root, IReadOnlyList<ISaveMigration> chain, int currentVersion); // test seam
}
```
`Apply` semantics: read `root["SaveVersion"]` as int (missing → 0). If `> currentVersion` throw `SaveTooNewException`. Otherwise apply each migration in `chain` (ordered ascending by `FromVersion`) whose `FromVersion >= fileVersion`, then set `root["SaveVersion"] = currentVersion` and return `root`.

### SaveJsonContext.cs
```csharp
namespace TheHaunt.Core;
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GameData))]
public sealed partial class SaveJsonContext : JsonSerializerContext { }
```

## 2. src/Systems — the three autoloads (files: GameState.cs, Clock.cs, SaveService.cs)

### GameState.cs
```csharp
namespace TheHaunt.Systems;

public partial class GameState : Node
{
    public enum Phase { Playing, Paused, Dialogue, Cutscene, Sleeping }

    public static GameState Instance { get; private set; } = null!;
    public Phase Current { get; private set; } = Phase.Playing;
    public event Action<Phase, Phase>? StateChanged;          // (from, to)

    // Consumers use these derived queries — NEVER compare the enum directly for behavior gating.
    public bool ClockRuns { get; }          // Current == Phase.Playing
    public bool PlayerHasControl { get; }   // Current == Phase.Playing

    public void TransitionTo(Phase next);
}
```
- `_EnterTree`: `Instance = this; ProcessMode = ProcessModeEnum.Always;`
- `TransitionTo`: no-op (return) if `next == Current`. `GetTree().Paused = (next == Phase.Paused)` — tree pause is used EXCLUSIVELY for the Paused phase; Dialogue/Cutscene/Sleeping never touch tree pause (clock stops via `ClockRuns`, player no-ops via `PlayerHasControl`). Fire `StateChanged(from, next)` last.

### Clock.cs
```csharp
namespace TheHaunt.Systems;

public partial class Clock : Node
{
    public static Clock Instance { get; private set; } = null!;
    public ClockModel Model { get; } = new();
    public GameTime Now { get; }                       // => Model.Now

    // Forwarded 1:1 from Model (wired once in _EnterTree; Model shares the Clock's lifetime):
    public event Action<GameTime>? MinuteTicked;
    public event Action<GameTime>? TenMinuteTicked;
    public event Action<GameTime>? HourTicked;
    public event Action<GameTime>? DayEnded;
    public event Action<GameTime>? DayStarted;

    public void AdvanceMinutes(int minutes);           // => Model
    public void AdvanceToDayStart();                   // => Model
    public void SetTime(GameTime time);                // => Model
}
```
- `_Process(double delta)`: `if (GameState.Instance.ClockRuns) Model.Accumulate(delta);`

### SaveService.cs
```csharp
namespace TheHaunt.Systems;

public enum LoadResult { Ok, NoFile, Corrupt, TooNew }

public partial class SaveService : Node
{
    public static SaveService Instance { get; private set; } = null!;
    public static string DefaultSlot { get; set; } = "save1";   // tests point this at a test slot
    public static string SaveDirectory { get; }                 // ProjectSettings.GlobalizePath("user://saves/")
    public GameData Current { get; private set; }               // never null; initialized to GameData.NewGame()

    public event Action? BeforeSave;
    public event Action? AfterLoad;   // fires after Load, DeserializeFrom, AND NewGame (UI refresh hook)

    public void Register(IPersistentSystem system);     // no duplicates
    public void Unregister(IPersistentSystem system);
    public void NewGame();
    public bool Save(string? slot = null);              // slot ?? DefaultSlot; false + GD.PushError on failure
    public LoadResult Load(string? slot = null);
    public bool SaveFileExists(string? slot = null);
    public string SerializeToString();                  // serialize Current ONLY — no WriteState/BeforeSave
    public void DeserializeFrom(string json);           // full load path minus file IO (throws on bad/too-new)
}
```
- `Load` failure semantics (data-loss protection): on Corrupt or TooNew, the unreadable file is
  QUARANTINED — renamed to `<slot>.json.bad` / `<slot>.json.toonew` (counter-suffixed on collision)
  — so a later `Save` to the slot can never destroy it. `Current` stays untouched; the caller
  decides how to proceed (Main starts a new game).
- `DeserializeFrom` validates semantics before the swap: null `Player`/`Maps` are replaced with
  defaults, negative `TotalMinutes` throws. Parseable-but-invalid data must never half-apply.
- Save path for slot: `Path.Combine(SaveDirectory, slot + ".json")`.
- `Save`: for each registered system `WriteState(Current)`; `Current.TotalMinutes = Clock.Instance.Now.TotalMinutes`; fire `BeforeSave`; serialize via `SaveJsonContext.Default.GameData`; `Directory.CreateDirectory(SaveDirectory)`; write to `path + ".tmp"` then `File.Move(tmp, path, overwrite: true)` (atomic); return true.
- `DeserializeFrom`: `JsonNode.Parse(json)` → `SaveMigrations.Apply(node)` → deserialize typed → `RebuildIndex()` on every `MapState` → swap `Current` → `Clock.Instance.SetTime(new GameTime(Current.TotalMinutes))` → each registered system `ReadState(Current)` → fire `AfterLoad`. On any exception before the swap, `Current` must remain untouched.
- `Load`: read file text, delegate to `DeserializeFrom`; false + `GD.PushError` on failure.
- `NewGame`: `Current = GameData.NewGame()`; `Clock.Instance.SetTime(new GameTime(0))`; each registered `ReadState(Current)`; fire `AfterLoad`.

## 3. src/World (files: MapRoot.cs, TestMap.cs, IInteractable.cs, Bed.cs, Sign.cs)

### MapRoot.cs
```csharp
namespace TheHaunt.World;

public partial class MapRoot : Node2D
{
    public const int TileSize = 16;
    [Export] public string MapId { get; set; } = "";

    public TileMapLayer? Ground { get; }                      // GetNodeOrNull<TileMapLayer>("Ground")
    public virtual Rect2 GetCameraLimits();                   // Ground used rect * TileSize; fallback Rect2(0,0,640,360)
    public Vector2 GetSpawn(string id = "default");           // Marker2D at $"Spawns/{id}"; fallback: camera-limits center
    public virtual void ApplyState(MapState state) { }        // hydrate visuals from the model after instancing
}
```

### TestMap.cs — programmatic placeholder map, 40x30 tiles
```csharp
namespace TheHaunt.World;
public partial class TestMap : MapRoot { /* builds everything in _Ready */ }
```
`_Ready` builds, in order:
1. A runtime `TileSet` (tile size 16x16, ONE `TileSetAtlasSource` with a procedural atlas texture of six 16x16 tiles, atlas coords `(i, 0)`): `0` grass A `#4a7c3a`, `1` grass B `#457539`, `2` grass C `#4f823d`, `3` dirt `#8a6a45`, `4` water `#3a6ea5`, `5` stone `#7a7a7a`. Add a few deterministic darker/lighter pixels per tile for texture. One physics layer (collision layer 1, mask 0): water and stone get a full-square collision polygon `(-8,-8) (8,-8) (8,8) (-8,8)`. One custom data layer `"walkable"` (bool): true for grass/dirt, false for water/stone.
2. `Ground` (TileMapLayer, child named exactly "Ground"): all 40x30 cells; grass variant via hash `(x * 7 + y * 13) % 3`; dirt rectangle at x 18..24, y 12..17.
3. `Obstacles` (TileMapLayer, "Obstacles", same TileSet): water on the 2-tile-thick border ring; stone at fixed coords `(5,5) (15,20) (30,10) (25,22) (10,18) (33,25) (18,6) (28,15) (6,24) (35,7) (22,3) (13,13)`.
4. `Spawns` (Node2D) with `default` (Marker2D) at tile (20, 15) center = `(328, 248)`.
5. `Interactables` (Node2D) with a `Bed` at tiles (8,8)-(8,9) (Area2D position = footprint center `(136, 152)`) and a `Sign` at tile (12,8) (center `(200, 136)`), sign message: `"Placeholder sign. Real text comes later."`

### IInteractable.cs
```csharp
namespace TheHaunt.World;
public interface IInteractable
{
    string PromptText { get; }
    bool CanInteract(Node2D interactor);
    void Interact(Node2D interactor);
}
```

### Bed.cs — `public partial class Bed : Area2D, IInteractable`
- `PromptText` → `"Sleep"`. `CanInteract` → `GameState.Instance.Current == GameState.Phase.Playing`. `Interact` → `GameState.Instance.TransitionTo(GameState.Phase.Sleeping)` (Main owns the actual sleep flow).
- `_Ready` builds: procedural Sprite2D (16x32 bed: frame `#8a5a3a`, blanket `#b03a3a`, pillow `#e8e4f0`), CollisionShape2D (Rect 16x32), plus a `StaticBody2D` child (collision layer 1, mask 0, Rect 14x30) so it blocks movement.
- Area2D config: `CollisionLayer = 2`, `CollisionMask = 0`, `Monitorable = true`.

### Sign.cs — `public partial class Sign : Area2D, IInteractable`
- `[Export] public string Message { get; set; } = "";` `PromptText` → `"Read"`. `CanInteract` → Playing phase.
- `Interact`: shows its own floating `Label` child (text = Message, centered ~20 px above, `Scale = (0.5, 0.5)`) for 3 seconds via `GetTree().CreateTimer(3.0)`, then hides. Self-contained — no UI/dialogue coupling in the foundation.
- `_Ready` builds: procedural Sprite2D (16x16 sign: post + board `#9a7a4a`), CollisionShape2D (Rect 16x16), StaticBody2D child (layer 1, Rect 12x10), the hidden Label.
- Area2D config: `CollisionLayer = 2`, `CollisionMask = 0`, `Monitorable = true`.

## 4. src/Player (files: PlayerController.cs, InteractionProbe.cs)

### PlayerController.cs
```csharp
namespace TheHaunt.Player;

public partial class PlayerController : CharacterBody2D, IPersistentSystem
{
    public const float MoveSpeed = 80f;                       // px/sec
    public int Facing { get; private set; }                   // 0=down 1=left 2=right 3=up
    public InteractionProbe Probe { get; private set; } = null!;

    public void ApplyCameraLimits(Rect2 limits);              // sets Camera2D Limit* (cast to int)
    public void WriteState(GameData data);                    // X/Y = GlobalPosition, Facing, HasPosition = true
    public void ReadState(GameData data);                     // if HasPosition: GlobalPosition + Facing from data
}
```
- `_EnterTree`: `SaveService.Instance.Register(this)`. `_ExitTree`: `Unregister(this)`.
- `_Ready` builds children in code: `Sprite2D` (procedural 16x22 texture per facing — hair `#5a4a3a`, skin `#e8c8a0`, tunic `#4a6ab0`; eyes visible facing down, offset left/right, hidden facing up; sprite `Position = (0, -3)`), `CollisionShape2D` (RectangleShape2D 12x8 at `(0, 6)` — feet), `Probe = new InteractionProbe()`, `Camera2D` (`PositionSmoothingEnabled = false`, default zoom).
- Body: `CollisionLayer = 1`, `CollisionMask = 1`.
- `_PhysicsProcess`: if `!GameState.Instance.PlayerHasControl` → `Velocity = Vector2.Zero; MoveAndSlide(); return;`. Else: `var input = Input.GetVector("move_left", "move_right", "move_up", "move_down")`; `Velocity = input * MoveSpeed`; `MoveAndSlide()`. If input non-zero, update `Facing` by dominant axis (|x| >= |y| → left/right else up/down), swap sprite texture, `Probe.SetFacing(Facing)`. Then: `if (Input.IsActionJustPressed("interact")) Probe.TryInteract(this);`

### InteractionProbe.cs
```csharp
namespace TheHaunt.Player;

public partial class InteractionProbe : Area2D
{
    public IInteractable? Focused { get; private set; }
    public event Action<IInteractable?>? FocusChanged;   // fired only on change

    public void SetFacing(int facing);                   // Position = feet (0,6) + dir * 14
    public void TryInteract(Node2D interactor);          // if Focused != null && CanInteract -> Interact
}
```
- `_Ready`: CollisionShape2D (CircleShape2D radius 8), `Monitoring = true`, `Monitorable = false`, `CollisionLayer = 0`, `CollisionMask = 2`. Initial facing down.
- `_PhysicsProcess`: poll `GetOverlappingAreas()` (robust against signal-order issues), pick the nearest node implementing `IInteractable` by distance to the probe; if it differs from `Focused`, update and fire `FocusChanged(Focused)`.

## 5. src/UI (files: Hud.cs, InteractionPrompt.cs, PauseMenu.cs, ScreenFade.cs)

All four are placed in `scenes/Main.tscn` by the integrator (see §6) and build their own child
controls in `_Ready`. All top-level UI roots: full-rect anchors, `MouseFilter = Ignore`
(PauseMenu switches to Stop while visible so buttons work).

### Hud.cs — `public partial class Hud : Control`
- Top-right `PanelContainer` → `VBoxContainer` → date `Label` + time `Label`.
- Updates from `Clock.Instance.Now` via `ToDateString()` / `ToClockString()`. Subscribes `Clock.MinuteTicked`, `Clock.DayStarted`, `SaveService.AfterLoad`; initial update in `_Ready`; unsubscribes in `_ExitTree`.

### InteractionPrompt.cs — `public partial class InteractionPrompt : Control`
```csharp
public void Bind(InteractionProbe probe);   // called by Main; unbinds any previous probe
```
- Bottom-center `Label`. On `FocusChanged`: hidden when null, else text `$"[E] {focused.PromptText}"`. Unsubscribes in `_ExitTree`.

### PauseMenu.cs — `public partial class PauseMenu : Control`
- `ProcessMode = Always` (set by integrator in the scene). Starts hidden.
- Children: dim full-rect ColorRect `(0,0,0,0.5)`, centered Panel with VBox: "Paused" label, `Resume`, `Save`, `Quit` buttons, and a small feedback label.
- `_UnhandledInput`: on `pause` action pressed → toggle: Playing → `TransitionTo(Paused)`; Paused → `TransitionTo(Playing)`; then `GetViewport().SetInputAsHandled()`. Ignore in other phases.
- Visibility is driven ONLY by `GameState.StateChanged` (visible iff `to == Paused`) — no direct Show/Hide in the toggle path. Subscribe `_Ready`, unsubscribe `_ExitTree`.
- Resume → `TransitionTo(Playing)`. Save → `SaveService.Instance.Save()`, feedback label "Saved." on success. Quit → `GetTree().Quit()`.

### ScreenFade.cs — `public partial class ScreenFade : ColorRect`
```csharp
public async Task FadeOut(double seconds = 0.4);   // to opaque black
public async Task FadeIn(double seconds = 0.4);    // back to transparent
```
- Full-rect, black, initial alpha 0, `MouseFilter = Ignore`. Tween `"color:a"`; `tween.SetPauseMode(Tween.TweenPauseMode.Process)`; `await ToSignal(tween, Tween.SignalName.Finished)`.

## 6. Integration (owned by the integrator — reference for everyone else)

`scenes/Main.tscn` node tree (scripts in parentheses):
```
Main (Node2D, src/Main.cs)
├── World (Node2D, process_mode = Pausable)
│   ├── MapHost (Node2D)                      ← exactly one MapRoot child at runtime
│   └── Player (CharacterBody2D, PlayerController)
└── UI (CanvasLayer, process_mode = Always)
    ├── Hud (Control, Hud)
    ├── InteractionPrompt (Control, InteractionPrompt)
    ├── PauseMenu (Control, PauseMenu, process_mode = Always)
    └── ScreenFade (ColorRect, ScreenFade)
```
`Main._Ready`: wires `InteractionPrompt.Bind(player.Probe)`; calls `Load()` and on any result
other than `Ok` calls `NewGame()` explicitly (never rely on the field-initializer default — NewGame
runs Clock.SetTime/ReadState/AfterLoad; unreadable files were already quarantined by Load);
instances `TestMap` into MapHost; `ApplyState`; places player at `GetSpawn()` when `!HasPosition`;
applies camera limits; subscribes `GameState.StateChanged` — on `Sleeping` runs: fade out →
`Clock.AdvanceToDayStart()` → `SaveService.Save()` → fade in → `TransitionTo(Playing)`, with the
whole flow in try/catch/finally so a failure surfaces via GD.PushError and the phase always
returns to Playing.

Autoloads (project.godot, this order): `GameState`, `Clock`, `SaveService` → `*res://src/Systems/<Name>.cs`.

## 7. src/Tests + scenes/tests (files: SimTestAttribute.cs, TestFailedException.cs, TestContext.cs, TestRunner.cs, CalendarTests.cs, ClockTests.cs, SaveTests.cs, IntegrationTests.cs, fixtures/v1_minimal.json, scenes/tests/TestRunner.tscn)

### Harness
```csharp
namespace TheHaunt.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SimTestAttribute : Attribute { }

public sealed class TestFailedException : Exception { public TestFailedException(string message); }

public sealed class TestContext
{
    public Node Host { get; }          // the TestRunner node (use to add/remove test scene instances)
    public SceneTree Tree { get; }
    public void Assert(bool condition, string message);                    // throws TestFailedException
    public void AssertEqual<T>(T expected, T actual, string label);
    public Task WaitFrames(int count);                                     // awaits process_frame count times
    public Task<bool> WaitUntil(Func<bool> condition, double timeoutSeconds = 5.0);  // poll per frame
}
```
- Tests are `[SimTest] public static void Name(TestContext t)` or `public static async Task Name(TestContext t)`, in classes under `TheHaunt.Tests`.
- `TestRunner.cs` (root script of `scenes/tests/TestRunner.tscn`, a single Node): in `_Ready` (async): set `SaveService.DefaultSlot = "test_autosave"`; delete any `test_*.json` in the save directory; reflection-discover `[SimTest]` methods in the executing assembly; **zero discovered tests = failure**; also fail if fewer than 14 (guards against silent discovery breakage). Run sequentially in name-sorted order, catching per-test exceptions. Print exactly one line per test: `PASS <Class.Method>` or `FAIL <Class.Method>: <message>`; then `RESULT: <n> passed, <m> failed`; delete `test_*.json` saves again; `GetTree().Quit(failCount == 0 ? 0 : 1)`.
- Any test that mutates global state (clock time, Current save data, game phase) must restore it before returning — easiest: `SaveService.Instance.NewGame()` and `GameState.Instance.TransitionTo(Phase.Playing)` in a finally.

### Required tests (exact assertions where given)
1. `Calendar_ClockStrings`: t=0 → "6:00 AM"; +360 → "12:00 PM"; +1080 → "12:00 AM"; +1140 → "1:00 AM"; +1199 → "1:59 AM".
2. `Calendar_Dates`: t=0 → "Mon. Spring 1, Year 1"; `StartOfDay(27)` → Spring 28; `StartOfDay(28)` → Summer 1; `StartOfDay(111)` → Winter 28 Year 1; `StartOfDay(112)` → Spring 1 Year 2; weekday of DayIndex 7 == Monday.
3. `Clock_TickEventCadence`: fresh `ClockModel`, `AdvanceMinutes(61)` → exactly 61 MinuteTicked, 6 TenMinuteTicked (at MinuteOfDay 10,20,30,40,50,60), 1 HourTicked (60).
4. `Clock_EndOfDayClamp`: `SetTime(1195)`; `AdvanceMinutes(10)` → Now.MinuteOfDay == 1199, exactly 4 minute ticks, zero DayEnded/DayStarted; further `AdvanceMinutes(5)` fires nothing.
5. `Clock_DayRollover`: from clamped end of day, `AdvanceToDayStart()` → event order exactly [DayEnded(old), DayStarted(new)], new DayIndex = old + 1, MinuteOfDay == 0.
6. `Clock_CatchupEquivalence`: model A `AdvanceMinutes(50)` vs model B 50 × `Accumulate(0.7)` → identical `Now` and identical ordered event logs. Also: one `Accumulate(1000)` steps exactly `MaxMinutesPerFrame` minutes.
7. `Save_RoundTrip`: construct GameData with 3 TileRecords + 1 PlacedObjectRecord via `GetMap("m").SetTile(...)`; serialize; `DeserializeFrom`; assert field equality and `GetTile` works post-load (index rebuilt).
8. `Save_TooNewRefused`: JSON with `"SaveVersion": 999` → `DeserializeFrom` throws `SaveTooNewException`; `Current` reference unchanged.
9. `Save_MigrationChainApplies`: fake `ISaveMigration` (FromVersion 0) mutating a field, applied via the 3-arg `Apply` overload on version-0 JSON → mutation present, `SaveVersion` bumped.
10. `Save_FixtureV1Loads`: load `res://src/Tests/fixtures/v1_minimal.json` via `FileAccess`, `DeserializeFrom` → assert player X/Y/Facing and one tile record survive.
11. `Save_NoGodotTypesInDtos`: walk the public property type graph from `GameData` (recurse into List<>/Dictionary<,> type args); assert no type from the GodotSharp assembly.
12. `Save_PerfBudget`: 5000 synthetic TileRecords in Current → `SerializeToString()` under 100 ms (Stopwatch).
13. `Save_AtomicFileWritten`: set a sentinel clock time; `Save("test_slot_a")` → `test_slot_a.json` exists, no `.tmp` remains, and loading the file back round-trips the sentinel (disk bytes are real, not just present).
13b. `Save_LoadFailureQuarantines`: a corrupt file → `Load` returns Corrupt, `Current` untouched, file renamed to `.bad` with bytes intact, and a subsequent `Save` to the slot leaves the quarantine alone; a `SaveVersion:999` file → TooNew + `.toonew`; a missing file → NoFile.
14. `Events_MapSwapStress`: 50 × { instance `new TestMap()`, add under Host, wait 1 frame, `Clock.Instance.AdvanceMinutes(10)`, free, wait 1 frame } → no exception (catches leaked C# event subscriptions on freed nodes).
15. `Integration_MainBootAndSleep`: instance `res://scenes/Main.tscn` under Host; `WaitFrames(5)`; assert `World/Player` exists; record DayIndex; `GameState.Instance.TransitionTo(Phase.Sleeping)`; `WaitUntil(day advanced && phase == Playing, 10)`; assert autosave file for `DefaultSlot` exists; free Main; cleanup.
16. `Interaction_ProbeFindsBed`: with Main instanced (fresh instance), teleport player to bed position + `(0, 28)`, `probe.SetFacing(3)` (up), `WaitUntil(probe.Focused != null, 2)`; assert `Focused.PromptText == "Sleep"`; free Main; cleanup.

### fixtures/v1_minimal.json
```json
{"SaveVersion":1,"TotalMinutes":600,"Player":{"MapId":"test_farm","X":100,"Y":120,"Facing":2,"HasPosition":true},"Maps":{"test_farm":{"Tiles":[{"X":3,"Y":4,"Kind":"tilled","CropId":null,"GrowthDay":0,"LastWateredDay":-1}],"Objects":[]}}}
```

## 8. Godot 4.7 C# API notes (use these exact APIs)

- **TileMapLayer** (NOT the deprecated TileMap): `layer.TileSet = tileSet; layer.SetCell(new Vector2I(x, y), sourceId: 0, atlasCoords: new Vector2I(i, 0));`
- **Runtime TileSet**:
  ```csharp
  var ts = new TileSet { TileSize = new Vector2I(16, 16) };
  ts.AddPhysicsLayer();                        // index 0
  ts.SetPhysicsLayerCollisionLayer(0, 1);      // world layer
  ts.SetPhysicsLayerCollisionMask(0, 0);
  ts.AddCustomDataLayer();                     // index 0
  ts.SetCustomDataLayerName(0, "walkable");
  ts.SetCustomDataLayerType(0, Variant.Type.Bool);
  var src = new TileSetAtlasSource { Texture = atlasTexture, TextureRegionSize = new Vector2I(16, 16) };
  ts.AddSource(src, 0);
  src.CreateTile(new Vector2I(i, 0));
  var td = src.GetTileData(new Vector2I(i, 0), 0);
  td.SetCustomData("walkable", true);
  td.SetCollisionPolygonsCount(0, 1);          // physics layer 0, one polygon
  td.SetCollisionPolygonPoints(0, 0, new[] { new Vector2(-8,-8), new Vector2(8,-8), new Vector2(8,8), new Vector2(-8,8) });
  ```
- **Procedural textures**: `var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8); img.Fill(...); img.SetPixel(...); var tex = ImageTexture.CreateFromImage(img);` — works headless.
- **Awaiting**: `await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);` / `await ToSignal(tween, Tween.SignalName.Finished);`
- **Autoload pattern**: set `Instance` and `ProcessMode = ProcessModeEnum.Always` in `_EnterTree` (autoloads default to Inherit and would freeze under tree pause).
- **Input**: `Input.GetVector("move_left","move_right","move_up","move_down")`, `Input.IsActionJustPressed("interact")`, and in `_UnhandledInput` use `@event.IsActionPressed("pause")` + `GetViewport().SetInputAsHandled()`.
- Physics (bodies, areas, overlaps) runs headless — tests rely on it.
- `GD.Print` for test output; `GD.PushError`/`GD.PushWarning` for diagnostics.
