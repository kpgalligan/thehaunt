# The Haunt — Phase 2 Spec: Core Sim Loop (authoritative)

Extends `docs/foundation-spec.md` (still binding — §0 global rules apply unchanged).
Multiple implementers work in parallel against this document. **Public signatures are
EXACT.** When modifying an existing file, READ it fully first and keep every existing
behavior and test green. Report deviations; do not invent contracts.

Scope: items/inventory/hotbar, tools, farming vertical slice, stamina, money + shipping
bin, v1→v2 save migration. Content stays lore-neutral (turnip, green bean — no lore names).

## 0. New global facts

- Save schema goes to **SaveVersion 2**. TileRecord: ZERO changes. "Watered today" =
  `LastWateredDay == dayIndex`, never a bool (§ foundation).
- New autoload **#4: `WorldSim`** (after SaveService) — the single gameplay-mutation bus.
  Every Phase-2 model write flows through it; UI subscribes to ITS events (never to events
  on Core objects — SaveService swaps `Current` wholesale on load).
- New input actions (integrator-owned, in project.godot): `use_tool` (mouse left + C),
  `hotbar_1`..`hotbar_10` (keys 1-9, 0), `hotbar_next` / `hotbar_prev` (wheel down/up).
- Facing/tile math unchanged: tile (x,y) center = (x*16+8, y*16+8); feet = GlobalPosition + (0,6).
- Item ids are strings; defs live in code. Unknown ids in save data are ALWAYS preserved
  (render as '?' placeholder), never dropped — item deletion is data loss.

## 1. src/Core additions (agent: core) — pure C#, no `using Godot`

### ItemDef.cs (ItemDef + the two enums share this file)
```csharp
namespace TheHaunt.Core;
public enum ItemCategory { Tool, Seed, Crop, Material }
public enum ToolKind { Hoe, WateringCan, Scythe }
public sealed record ItemDef(
    string Id,
    string Name,
    ItemCategory Category,
    int MaxStack,
    int SellPrice,              // 0 = unsellable
    string IconColor,           // "#rrggbb" — UI tints procedural icons with this
    ToolKind? Tool = null,
    int StaminaCost = 0,
    string? PlantsCropId = null);
```

### ItemDefs.cs
```csharp
public static class ItemDefs
{
    public static IReadOnlyDictionary<string, ItemDef> All { get; }
    public static ItemDef Get(string id);         // throws KeyNotFoundException — code bug
    public static ItemDef? TryGet(string id);     // null-tolerant — save-file ids
}
```
Catalog (exact): `hoe` (Tool, max 1, sell 0, "#8a5a3a", Hoe, cost 2) · `watering_can`
(Tool, 1, 0, "#6a8ab0", WateringCan, 1) · `scythe` (Tool, 1, 0, "#9a9a9a", Scythe, 1) ·
`turnip_seeds` (Seed, 99, 10, "#c8b060", plants "turnip") · `greenbean_seeds` (Seed, 99,
15, "#7ab060", plants "greenbean") · `turnip` (Crop, 99, 35, "#d8c8e8") · `greenbean`
(Crop, 99, 40, "#4a9a4a").

### CropDef.cs / CropDefs.cs
```csharp
public sealed record CropDef(
    string Id, string Name, int[] StageDays, string HarvestItemId,
    int HarvestCount = 1, int RegrowDays = 0)   // 0 = single-harvest
{
    public int TotalDays { get; }               // StageDays.Sum()
    public int StageForDay(int growthDay);      // see semantics below
}
public static class CropDefs
{
    public static IReadOnlyDictionary<string, CropDef> All { get; }
    public static CropDef Get(string id);
    public static CropDef? TryGet(string id);
}
```
`StageForDay`: stage `s` covers growth days `[prefixSum(s), prefixSum(s+1))`;
`growthDay >= TotalDays` returns `StageDays.Length` (the mature column). Examples for
turnip `{1,1,1,2}` (TotalDays 5): g0→0, g1→1, g2→2, g3→3, g4→3, g5→4 (mature).
Catalog: `turnip` (StageDays {1,1,1,2}, harvest "turnip", 1, no regrow) · `greenbean`
(StageDays {1,1,2,2}, harvest "greenbean", 1, RegrowDays 3).

### ItemStackRecord.cs
```csharp
public sealed class ItemStackRecord { public string ItemId { get; set; } = ""; public int Count { get; set; } }
```

### InventoryData.cs
```csharp
public sealed class InventoryData
{
    public const int Capacity = 10;                    // hotbar IS the inventory in v2
    public List<ItemStackRecord?> Slots { get; set; }  // init: Capacity nulls; null = empty; indices stable
    public int SelectedSlot { get; set; }              // 0..Capacity-1
    public ItemStackRecord? Selected { get; }          // SlotAt(SelectedSlot)
    public ItemStackRecord? SlotAt(int i);             // null when out of range
    public int Add(string itemId, int count);          // returns overflow NOT placed; tops up same-id stacks lowest-index-first, then empty slots
    public int Remove(string itemId, int count);       // returns count actually removed
    public int RemoveFromSlot(int slot, int count);    // returns count actually removed; nulls emptied stacks
    public bool TryConsumeSelected(int count);         // all-or-nothing from the selected stack
    public int CountOf(string itemId);
    public bool HasRoomFor(string itemId, int count);
    public void Normalize();                           // load repair — see rules
}
```
- Stack limits via `ItemDefs.TryGet(id)?.MaxStack ?? 1` (unknown ids: conservative 1).
- `Normalize()` rules (each individually load-bearing): PAD `Slots` to Capacity, **never
  trim** (forward compatibility — raising Capacity later must remain a constant change,
  not a migration); null out entries with `Count <= 0` or empty ItemId; KEEP unknown ids
  and over-stacks intact (never destroy items); clamp `SelectedSlot` to
  `Math.Min(Capacity, Slots.Count) - 1` (selection stays within the visible hotbar even
  when an over-capacity save preserves extra slots).

### StarterKit.cs
```csharp
public static class StarterKit { public static void Apply(PlayerData player); }
```
Slots 0-4: hoe×1, watering_can×1, scythe×1, turnip_seeds×15, greenbean_seeds×5; SelectedSlot 0.
Called ONLY by `GameData.NewGame()` — NEVER by the migration.

### FarmActions.cs
```csharp
public enum ActionOutcome { NoEffect, InvalidTarget, NotEnoughStamina, InventoryFull, Tilled, Planted, Watered, Harvested, Cleared }
public static class FarmActions
{
    public static ActionOutcome UseSelected(GameData data, string mapId, int x, int y, long today, bool terrainTillable);
}
```
All farming TileRecord mutations live here. Tile states: ABSENT (no record) → TILLED
(`Kind == "tilled"`, CropId null) → PLANTED (CropId set). Dispatch precedence:
1. **Mature crop on tile** (`GrowthDay >= def.TotalDays`) → harvest, regardless of selected
   item, stamina cost 0: `HasRoomFor(HarvestItemId, HarvestCount)` FIRST — if not,
   return `InventoryFull` with the tile bit-identical; else `Add` the harvest, then
   RegrowDays > 0 ? `GrowthDay = TotalDays - RegrowDays` : (`CropId = null; GrowthDay = 0`).
   Returns `Harvested`.
2. **Selected tool**: Hoe — only on ABSENT target with `terrainTillable` true → create
   `TileRecord { Kind = "tilled", LastWateredDay = -1 }` → `Tilled`. WateringCan — on any
   TILLED/PLANTED tile → `LastWateredDay = today` (idempotent; pre-watering empty soil
   counts for tonight) → `Watered`. Scythe — on PLANTED with a KNOWN CropDef (non-mature;
   mature is intercepted by rule 1; unknown crop ids are preserved → `NoEffect`) →
   `CropId = null; GrowthDay = 0`, Kind stays "tilled", no yield → `Cleared`.
3. **Selected seed** (`PlantsCropId != null`) on TILLED with no crop →
   `TryConsumeSelected(1)`, `CropId = PlantsCropId`, `GrowthDay = 0`,
   **LastWateredDay PRESERVED** (watering is the only writer) → `Planted`.
Anything else → `NoEffect`; off-map / not-tillable hoe target → `InvalidTarget`.
Per-action contract: validate target → check `Stamina >= ItemDef.StaminaCost` (else
`NotEnoughStamina`, no mutation) → mutate → deduct stamina. Stamina charged only on
effective use (refusals and NoEffect cost nothing).

### OvernightSim.cs
```csharp
public readonly record struct OvernightReport(int CropsGrown, long ShippingProceeds);
public static class OvernightSim
{
    public static OvernightReport Run(GameData data, long dayEnding);
}
```
`dayEnding` = the DayEnded payload's DayIndex (the day being closed — NEVER "now", which
is already next morning by the time views observe it). Fixed order, iterating EVERY
MapState (loaded or not):
1. Growth: each tile with a known CropId: `GrowthDay++` iff `LastWateredDay == dayEnding
   && GrowthDay < def.TotalDays`.
2. Shipping sale: `Player.Money += Σ SellPrice × Count` over ShippingBin stacks with known
   sellable defs; sold stacks removed; unknown ids SKIPPED and PRESERVED in the bin.
3. `Player.Stamina = Player.MaxStamina`.

### MigrationV1ToV2.cs
```csharp
public sealed class MigrationV1ToV2 : ISaveMigration { public int FromVersion => 1; public void Apply(JsonNode root); }
```
Writes LITERAL JSON, each key only if absent: `Player.Money = 500`, `Player.Stamina = 100`,
`Player.MaxStamina = 100`, `Player.Inventory = { "Slots": [hoe×1, watering_can×1,
scythe×1, turnip_seeds×15, greenbean_seeds×5, null, null, null, null, null],
"SelectedSlot": 0 }` (stacks as `{"ItemId": "...", "Count": n}`), `root.ShippingBin = []`.
Deliberately does NOT call ItemDefs/StarterKit/NewGame — migrations are frozen history.

### Modified: PlayerData.cs
Add: `public long Money { get; set; }` · `public int Stamina { get; set; }` ·
`public int MaxStamina { get; set; }` · `public InventoryData Inventory { get; set; } = new();`

### Modified: GameData.cs
Add: `public List<ItemStackRecord> ShippingBin { get; set; } = new();`
`NewGame()`: also set Money = 500, Stamina = MaxStamina = 100, `StarterKit.Apply(Player)`.

### Modified: SaveMigrations.cs
`CurrentVersion = 2`; `Chain = [new MigrationV1ToV2()]`. (Existing `FromVersion >=
fileVersion` semantics already skip v2 files — no double grant.)

## 2. src/Systems (agent: systems)

### NEW: WorldSim.cs — autoload #4
```csharp
namespace TheHaunt.Systems;
public partial class WorldSim : Node
{
    public static WorldSim Instance { get; private set; } = null!;

    public event Action? InventoryChanged;                 // slots OR selection changed
    public event Action<int, int>? StaminaChanged;         // (current, max)
    public event Action<long>? MoneyChanged;
    public event Action<OvernightReport>? OvernightCompleted;

    public void RegisterMap(MapRoot map);                  // called by MapRoot._EnterTree
    public void UnregisterMap(MapRoot map);
    public void SelectSlot(int slot);                      // clamp 0..9, persist to Inventory.SelectedSlot, fire InventoryChanged
    public ActionOutcome UseSelectedItem(Vector2I tile);
    public bool DepositSelectedToBin();
}
```
- `_EnterTree`: Instance/ProcessMode per foundation pattern; `Clock.Instance.DayEnded += OnDayEnded`.
  `_ExitTree`: unsubscribe.
- `UseSelectedItem`: resolve the registered map whose `MapId == Current.Player.MapId`
  (no map registered → `InvalidTarget`); `terrainTillable = map.IsTillable(tile.X, tile.Y)`;
  call `FarmActions.UseSelected(...today: Clock.Instance.Now.DayIndex...)`; on any
  tile-mutating outcome call `map.RefreshTile(tile.X, tile.Y, record)`; fire
  InventoryChanged/StaminaChanged as the outcome implies. Returns the outcome.
- `DepositSelectedToBin`: whole selected stack; refuse when empty or `SellPrice <= 0`
  (tools unshippable) → false; merge into `Current.ShippingBin` by id; null the slot;
  fire InventoryChanged; true.
- Overnight handling is split across the two day events: `OnDayEnded(endedDay)` runs
  `OvernightSim.Run(Current, endedDay.DayIndex)` (the model needs the day being closed);
  `OnDayStarted` does the full `ApplyState` repaint per registered map and fires
  OvernightCompleted(report)/MoneyChanged/StaminaChanged/InventoryChanged — the repaint
  must NOT happen during DayEnded because the wet/dry soil visual reads Clock.Now, which
  still shows the ending day at that point (wet overlays would not flip).
- Both fire synchronously inside `Clock.AdvanceToDayStart()`, i.e. BEFORE Main's autosave.

### Modified: SaveService.cs — pre-swap validation block in DeserializeFrom
`data.Player.Inventory ??= new InventoryData();` · `data.Player.Inventory.Normalize();` ·
`data.ShippingBin ??= new();` · ShippingBin repair mirroring Normalize: remove entries
that are null / null-or-empty ItemId / `Count <= 0` (a null element NREs the overnight
sale and softlocks sleep; a negative count mints negative money and corrupts the save) —
unknown-but-well-formed ids are KEPT · negative Money throws JsonException (TotalMinutes
precedent) · clamp Stamina into `[0, MaxStamina]` (and MaxStamina to >= 1).

## 3. src/World (agent: world)

### Modified: MapRoot.cs
Add:
```csharp
public virtual bool IsTillable(int x, int y) => false;
public virtual void RefreshTile(int x, int y, TileRecord? record) { }   // O(1) incremental visual update
public override void _EnterTree()  => WorldSim.Instance.RegisterMap(this);
public override void _ExitTree()   => WorldSim.Instance.UnregisterMap(this);
```
(Keep any existing _EnterTree/_ExitTree behavior; TestMap overrides must call base.)

### Modified: TestMap.cs
- Layer build order (child order = draw order): Ground → **FarmSoil** → **Crops** → Obstacles.
- `FarmSoil` (TileMapLayer): procedural 2-tile atlas — tilled-dry `#7a5a38` (darker worked
  soil with furrow pixels), tilled-wet `#5a4230`. No collision, no custom data.
- `Crops` (TileMapLayer): procedural atlas, one ROW per CropDef (row order = CropDefs
  iteration order, document it in code), one COLUMN per stage plus a final mature column
  (columns = StageDays.Length + 1). Simple readable pixels: sprout → bigger plant →
  mature with fruit color pop. Deterministic.
- Cell state is a PURE function of (TileRecord, todayIndex):
  FarmSoil cell = none (no record) | dry | wet (`Kind == "tilled" &&
  LastWateredDay == Clock.Instance.Now.DayIndex`); Crops cell = none (no CropId or unknown)
  | `(row of CropId, column StageForDay(GrowthDay))`.
- Override `IsTillable(x, y)`: Ground cell has `walkable` custom data true AND Obstacles
  cell is empty AND the tile is not an interactable footprint tile (bed (8,8)-(8,9), sign
  (12,8), shipping bin (10,8) — a reserved-tile set populated alongside placement;
  tilling under a sprite would render invisibly).
- Override `RefreshTile`: SetCell/EraseCell on FarmSoil + Crops for that coord — O(1).
- Override `ApplyState(MapState)`: clear FarmSoil + Crops, repaint from every TileRecord —
  the load-time and overnight full-rebuild path. (Call from _Ready too if state was
  applied before layers existed — Main calls ApplyState right after AddChild.)
- Interactables: add a **ShippingBin** at tile (10, 8), Area2D position (168, 136) — clear
  of Bed (8,8-9) and Sign (12,8).

### NEW: ShippingBin.cs — `public partial class ShippingBin : Area2D, IInteractable`
Pattern-copy Bed/Sign: `PromptText => "Ship"`; `CanInteract` → Playing phase; `Interact` →
`WorldSim.Instance.DepositSelectedToBin()`. Procedural 16×16 wooden crate sprite
(`#9a7a4a` frame, darker slats), CollisionShape2D 16×16, StaticBody2D blocker (layer 1,
12×12), area layer 2 / mask 0 / monitorable.

## 4. src/Player (agent: player)

### Modified: PlayerController.cs
```csharp
public Vector2I TargetTile();   // floor((GlobalPosition + (0,6)) / 16) + facing dir vector
```
Do NOT derive the target from the probe position (probe = feet + dir×14 rounds into the
player's own tile when feet%16 < 2).
- `_PhysicsProcess` (inside the PlayerHasControl branch): `use_tool` just-pressed AND a
  0.25 s cooldown (accumulate delta) → `WorldSim.Instance.UseSelectedItem(TargetTile())`;
  `hotbar_1`..`hotbar_10` just-pressed → `WorldSim.Instance.SelectSlot(n)`.
- `_UnhandledInput`: `hotbar_next`/`hotbar_prev` via `@event.IsActionPressed(...)` (mouse
  wheel press+release land in the same frame — IsActionJustPressed in _PhysicsProcess
  misses them) → SelectSlot((SelectedSlot ± 1 + 10) % 10), then SetInputAsHandled. Gate on
  PlayerHasControl.

## 5. src/UI (agent: ui)

All code-built from default-theme controls, `SetAnchorsAndOffsetsPreset` (NEVER
`SetAnchorsPreset` — see CLAUDE.md), every `+=` paired with `-=` in `_ExitTree`.

### NEW: HotbarUi.cs — `public partial class HotbarUi : Control`
Bottom-center HBox of 10 slot panels (~26 px): procedural 12×12 flat icon tinted by
`ItemDef.IconColor` (cache one ImageTexture per item id; unknown id → gray '?' placeholder
tile — never throw), count label (hidden for count <= 1), selection highlight (border or
brighter panel). Subscribes `WorldSim.InventoryChanged` + `SaveService.AfterLoad`; full
10-slot redraw per event (cheap at this scale).

### NEW: StaminaBar.cs — `public partial class StaminaBar : Control`
Bottom-left: a ProgressBar or code-drawn bar (~80×10) + no text. Green → amber under 25%.
Subscribes `WorldSim.StaminaChanged` + `SaveService.AfterLoad`; initial state in _Ready
from `SaveService.Instance.Current.Player`.

### Modified: Hud.cs
Append a money Label to the existing VBox (format `"{Money}g"`). Subscribes
`WorldSim.MoneyChanged` + existing `SaveService.AfterLoad` refresh. MinuteTicked usage
unchanged (clock display only).

### Modified: InteractionPrompt.cs
Bump the label's bottom margin from 24 to 56 so the prompt clears the new hotbar.

## 6. Integration (integrator-owned — reference)

- project.godot: `WorldSim="*res://src/Systems/WorldSim.cs"` appended to autoloads (order:
  GameState, Clock, SaveService, WorldSim); new input actions per §0.
- scenes/Main.tscn: UI children order becomes Hud, InteractionPrompt, HotbarUi, StaminaBar,
  PauseMenu, ScreenFade (menu + fade stay on top).
- Main.cs: unchanged (day-rollover sequence already correct: DayEnded → WorldSim overnight
  → DayStarted → autosave inside the fade).

## 7. src/Tests (agent: tests)

Modify existing files ONLY as listed; new tests go in new files: `InventoryTests.cs`
(Items_/Inventory_), `FarmTests.cs` (Farm_/Stamina_), `EconTests.cs` (Econ_),
migration/round-trip additions in `SaveTests.cs`, visual/loop additions in
`IntegrationTests.cs`. Bump TestRunner's minimum-discovered guard to 30. Global-state
restoration rules unchanged (NewGame + Playing in finally). Add frozen
`fixtures/v2_minimal.json` (v2 baseline for the future v3 test).

1. `Items_DefsValidate`: every seed's PlantsCropId resolves; every crop's HarvestItemId
   resolves; every StarterKit id resolves; all StageDays entries >= 1; for regrow crops
   `0 < RegrowDays <= TotalDays`; every Tool has MaxStack 1; IconColor parses as #rrggbb.
2. `Inventory_AddMergeOverflow`: Add tops up lowest-index stacks first, then empties;
   overflow returned when full; MaxStack respected; tools never stack.
3. `Inventory_NormalizeRepairs`: 3-slot list padded to 10 (never trimmed from 12 — assert
   over-capacity lists KEEP all 12); count<=0 nulled; unknown ids KEPT; SelectedSlot clamped.
4. `Farm_Transitions`: full legality matrix — hoe on virgin tillable/non-tillable/existing
   record; can on absent/tilled/planted; seed on absent/tilled/planted/mature; scythe on
   absent/tilled/planted; plant PRESERVES LastWateredDay; every refusal leaves the model
   bit-identical.
5. `Farm_OvernightGrowthExact`: water day D → Run(D) ⇒ GrowthDay 1; Run(D+1) unwatered ⇒
   still 1; watering twice same day ⇒ +1 only; serialize → deserialize → Run ⇒ exactly 1.
6. `Farm_RegrowCycle`: greenbean matures (6 watered nights), harvest ⇒ GrowthDay ==
   TotalDays − 3 and inventory +1; 3 more watered nights ⇒ mature again; GrowthDay never
   exceeds TotalDays.
7. `Farm_HarvestFullInventoryRefuses`: full inventory ⇒ InventoryFull, tile bit-identical;
   free a slot ⇒ Harvested adds exactly HarvestCount once.
8. `Stamina_RefuseAndRestore`: stamina below hoe cost ⇒ NotEnoughStamina, no mutation, no
   deduction; OvernightSim.Run restores to MaxStamina.
9. `Econ_DepositMovesNotCopies`: per-id conservation across inventory + bin through
   deposit and a serialize/deserialize round-trip; unsellable (tool) deposit refused.
10. `Econ_ShippingOvernight` (autoload path): deposit via WorldSim, Save + Load, sleep via
    Clock.AdvanceToDayStart ⇒ Money += exact sum, bin empty, StaminaChanged/MoneyChanged fired.
11. `Farm_UnloadedMapGrows`: watered crop written into a MapState for a map that is NEVER
    instanced; Clock.AdvanceToDayStart() ⇒ GrowthDay incremented (no MapRoot involved).
12. `Save_MigrationV1ToV2`: DeserializeFrom(the byte-frozen v1_minimal.json fixture) ⇒
    SaveVersion 2, Money 500, Stamina 100/100, exact starter slots, ShippingBin [] AND the
    v1 payload survives (X=100, Y=120, Facing=2, TotalMinutes=600, tile (3,4) "tilled",
    LastWateredDay -1); then SerializeToString → DeserializeFrom again ⇒ stack-for-stack
    identical (idempotent, no re-grant).
13. `Save_MigratedKitMatchesNewGame` (drift guard): migrated-v1 inventory equals
    `GameData.NewGame()` inventory slot-for-slot. If the starter kit ever changes this
    MUST fail — the fix is a conscious decision, never editing the frozen migration.
14. `Save_UnknownItemIdSurvives`: a save with `{"ItemId":"mystery_relic","Count":3}` loads,
    Normalize keeps it, round-trip preserves it exactly.
15. Extend `Save_RoundTrip`: Money, Stamina/MaxStamina, ShippingBin contents, null-holed
    Slots (e.g. slot 2 null between stacks), SelectedSlot.
16. `Visual_RebuildEqualsIncremental`: instance TestMap; drive till → plant → water via
    WorldSim.UseSelectedItem (set Player.MapId first, select the right slots); after EVERY
    step assert FarmSoil + Crops `GetCellAtlasCoords` match the pure cell-state function;
    then free the map, re-instance, ApplyState ⇒ identical cells; sleep ⇒ wet flips to dry
    everywhere and stages advance.
17. Extend `Events_MapSwapStress`: each of the 50 cycles also performs one
    `UseSelectedItem` and one `Clock.AdvanceToDayStart()` between instance and free — any
    leaked subscription or stale map registration must crash the run. (Adjust the final
    clock assertion accordingly — day count, not minute count.)
18. `Integration_FullFarmLoop`: boot Main; teleport onto tillable ground; till, plant,
    water via WorldSim; sleep (Sleeping transition) × enough nights watering each day;
    harvest; deposit to bin; sleep ⇒ Money increased by the exact sale sum; autosave exists.

## 8. Notes for implementers

- ProcessMode/Instance autoload pattern, Godot API notes, and test-harness contracts: see
  foundation spec §8. TileMapLayer `GetCellAtlasCoords(coords)` returns (-1,-1) for empty.
- WorldSim (Systems) referencing MapRoot (World) is an accepted namespace cycle within the
  single assembly — do not introduce an interface for it in v2.
- The scythe can never hit a MATURE crop (harvest-priority intercepts). Intentional.
- StageForDay and cell-state purity matter: tests recompute expected cells independently.
- Wet/dry soil visual uses Clock.Instance.Now.DayIndex at REFRESH time (view-side), while
  growth logic uses the DayEnded payload day (model-side). Do not mix them.
