# Phase 3b Spec — Store, Farmhouse & Chest, Overnight Report, Help Panel

Authoritative contract for Phase 3b. Signatures, ids, tile coordinates, and event orders
below are FROZEN — implementation agents code against them without renegotiation. Produced
by the 3-lens design panel + judge (2026-08-25), verified against the live codebase.

Scope (user-ordered): (1) a general store in town to buy seeds; (2) visible feedback that
shipped crops sold (money is already tracked AND displayed in the HUD — the sale itself was
silent); (3) a small farmhouse containing the bed, a table, and a storage chest; (4) a
toggleable controls panel on Tab.

## §0 Verified engine facts (trust these; do not re-derive)

- Minute 0 of a day = 6:00 AM. Meeting minute 720 = 6:00 PM. So 180 = 9:00 AM, 660 = 5:00 PM.
- `OvernightSim.Run` already sells the shipping bin (unknown/unsellable ids stay binned) and
  returns `OvernightReport(int CropsGrown, long ShippingProceeds)`; the only constructor call
  is in `OvernightSim.cs:48`; nothing deconstructs it. `WorldSim.OvernightCompleted` fires
  synchronously inside `AdvanceToDayStart` (screen is black) and has ZERO subscribers today.
- `Main.RunSleepFlow`: FadeOut → AdvanceToDayStart → Save → FadeIn → finally TransitionTo(Playing).
- `GameState`: ClockRuns and PlayerHasControl are both `Current == Playing`; CanStartDialogue is
  `Playing or Cutscene`; `GetTree().Paused = next == Paused` (exclusive). PauseMenu's
  `_UnhandledInput` else-returns for phases other than Playing/Paused.
- `StoryDirector.CheckTriggers` bails on `!PlayerHasControl`; `IntroRules.PendingBeat`'s
  CrewArrival term requires `activeMapId == MapIds.Farm` (strict equality) and beats re-pend —
  no day-equality terms. IntroRules needs ZERO changes for the new interiors.
- `MapRoot.ExpandToViewport` (MapRoot.cs:34) grows sub-viewport camera limits to 640x360
  centered — built for exactly the small-interior case.
- PlayerController's `_hadControlLastPhysicsFrame` guard (PlayerController.cs:86-107) swallows
  action presses on the first frame control returns. DialogueUi's `_openedFrame` guard
  (DialogueUi.cs:54) swallows the opening press. New modal UIs replicate BOTH patterns.
- Existing tests sleep via `GameState.TransitionTo(Phase.Sleeping)`, never via the Bed node.
  `SleepOneNight` helper: IntegrationTests.cs:592, five call sites (285, 318, 349, 435, 480).
  `Interaction_ProbeFindsBed` uses `BedPosition = (136, 152)` (line 13).
  Literal `SaveVersion == 3` pins: SaveTests.cs lines 487, 537, 653.
  `TravelTests.DocumentedSpawns` throws KeyNotFoundException for unlisted MapIds.All entries.
  NpcTests.cs:71 asserts every schedule placement map is in MapIds.All.
- Kevin's REAL save (v3, day 2, intro complete, 1025g) was scanned read-only on 2026-08-25:
  all 22 farm tiles sit in x18-25, y7-10 — CLEAR of the house footprint x4-9,y4-7, the stone
  relocation target (5,12), and the vacated bed tiles (8,8)/(8,9). Layout approved. NEVER
  write to that save.

## §1 Storage model & save v4

### 1.1 StackOps (new `src/Core/StackOps.cs`)

Shared stack algebra over `List<ItemStackRecord?>`, extracted from InventoryData with ZERO
behavior change (existing InventoryTests pin the refactor):

```csharp
public static class StackOps
{
    public static int MaxStackFor(string itemId);          // ItemDefs.TryGet(...)?.MaxStack ?? 1
    public static int Add(List<ItemStackRecord?> slots, string itemId, int count); // returns overflow; top-up lowest-index same-id first, then fill empties
    public static int CountOf(List<ItemStackRecord?> slots, string itemId);
    public static bool HasRoomFor(List<ItemStackRecord?> slots, string itemId, int count);
}
```

`InventoryData.Add/CountOf/HasRoomFor` become delegating wrappers. Remove/RemoveFromSlot/
TryConsumeSelected/Normalize stay as they are.

### 1.2 StorageData (new `src/Core/StorageData.cs`) and StorageIds (new `src/Core/StorageIds.cs`)

```csharp
public sealed class StorageData
{
    public List<ItemStackRecord?> Slots { get; set; } = new();
    public int Add(string itemId, int count);   // delegates to StackOps
    // Pads Slots to capacity when non-null (known ids only); NEVER trims over-capacity
    // saves; nulls degenerate entries (empty id / count <= 0); keeps unknown item ids
    // and over-stacks verbatim.
    public void Normalize(int? capacity);
}

public static class StorageIds
{
    public const string FarmHouseChest = "farm_house_chest";
    public static int? CapacityOf(string id);   // FarmHouseChest => 20; unknown => null
}
```

Chest capacity is 20 (2 rows of 10, hotbar-width). [KEVIN] Growing it later is a constant
change, never a migration.

### 1.3 GameData

```csharp
public Dictionary<string, StorageData> Storages { get; set; } = new();
public StorageData GetStorage(string id);   // lazy-create mirroring GetMap; Normalize(StorageIds.CapacityOf(id)) on create
```

`NewGame()` does NOT change — an empty Storages dict is correct; the chest materializes on
first open. Both existing drift guards ship UNMODIFIED (they compare kit/story fields only).

### 1.4 Migration v3→v4

- New `src/Core/MigrationV3ToV4.cs`, frozen literal mirroring V2ToV3 exactly:
  FromVersion=3; `if (root["Storages"] is null) root["Storages"] = new JsonObject();` — nothing else.
- `SaveMigrations.CurrentVersion = 4`; Chain gains `new MigrationV3ToV4()`.
- `SaveService.DeserializeFrom` pre-swap repairs, appended after the existing intro-stamp
  clamp: `data.Storages ??= new();` drop the `""` key; null values → `new StorageData()`;
  then per entry `Normalize(StorageIds.CapacityOf(key))` — unknown storage KEYS round-trip
  un-padded (their capacity is not ours to invent) with only degenerate-entry nulling.
- `v3_minimal.json` is byte-frozen (it IS the v4 migration's input) — never touched.
- New byte-frozen fixture `src/Tests/fixtures/v4_minimal.json` (§7).

### 1.5 WorldSim transfer API (phase-free model ops, like DepositSelectedToBin)

```csharp
public event Action<string>? StorageChanged;                      // storage id, per mutation
public bool TransferToStorage(string storageId, int inventorySlot);
public bool TransferToInventory(string storageId, int storageSlot);
```

Whole-stack, partial-on-overflow, loss/dupe-proof ordering: read the source stack (null →
return false); null the source slot; `overflow = StackOps.Add(dest.Slots, id, count)`; if
`overflow == count` restore the original stack object to the source slot and return false —
NO events; else if `overflow > 0` write `new ItemStackRecord { ItemId, Count = overflow }`
into the just-vacated source slot (cannot collide). On any movement: fire
`StorageChanged(storageId)` THEN `InventoryChanged`; return true. Unknown item ids transfer
normally at maxstack 1 — never destroyed.

## §2 Menu phase & UI sessions

### 2.1 GameState

`Phase` gains `Menu` — the ONLY GameState change. Mechanically verified: ClockRuns and
PlayerHasControl are false in Menu (clock frozen, player frozen), CanStartDialogue refuses,
tree is NOT paused, PauseMenu/Bed/Sign/ShippingBin/Door/RequestTravel/CheckTriggers all
refuse via existing gates. Document as a foundation-spec amendment in §9b at ship.

### 2.2 WorldSim session ownership (mirrors the dialogue session)

```csharp
public string? OpenStorageId { get; private set; }
public string? OpenShopId    { get; private set; }
public event Action<string>? StorageOpened;   // storage id
public event Action?         StorageClosed;
public event Action<string>? ShopOpened;      // catalog id
public event Action?         ShopClosed;

public bool OpenStorage(string storageId);  // gate: PlayerHasControl && both session ids null → set id, TransitionTo(Menu), fire StorageOpened
public void CloseStorage();                 // no-op if null; null id, TransitionTo(Playing), fire StorageClosed
public bool OpenShop(string catalogId);     // same shape; catalog must exist in ShopCatalog
public void CloseShop();
```

`WorldSim.OnAfterLoad` extension (mirrors the dialogue-strand fix, BEFORE SyncNpcsNow): if
either session id is non-null → null both, `TransitionTo(GameState.Phase.Playing)`, fire the
matching Closed event(s). Flag-based, no phase comparison.

### 2.3 Chest node (new `src/World/Chest.cs`)

Area2D + IInteractable, layer 2, ShippingBin's visual pattern (16x16 procedural chest sprite
+ StaticBody blocker). `[Export] string StorageId` defaulting to `StorageIds.FarmHouseChest`;
`PromptText => "Open"`; `CanInteract => GameState.Instance.PlayerHasControl`;
`Interact => WorldSim.Instance.OpenStorage(StorageId)`.

### 2.4 ChestUi (new `src/UI/ChestUi.cs`, Main.tscn UI child)

Code-built. Visibility driven ONLY by StorageOpened/StorageClosed (WorldSim guarantees a
Closed fires on AfterLoad-with-open-session). On open: record `_openedFrame =
Engine.GetProcessFrames()` and GrabFocus chest slot 0. Layout: centered panel; chest grid
2x10 above the 10-slot inventory row; slots are focus Buttons (colored rect + count label,
'?' for unknown ids; arrow keys / ui_* navigate, mouse works). A press on a focused slot
(Button.Pressed, or `interact` in `_UnhandledInput`) moves the WHOLE stack via §1.5 — what
fits moves, remainder stays, a brief "Full" flash label on total refusal; nothing is ever
dropped. `_UnhandledInput` ignores `_openedFrame`'s frame; Esc (`pause` action) closes:
SetInputAsHandled → `WorldSim.CloseStorage()`. Repaints on StorageChanged/InventoryChanged.
Every `+=` has a matching `-=` in `_ExitTree`.

## §3 Shop

### 3.1 Core (new `src/Core/ShopHours.cs`, new `src/Core/ShopCatalog.cs`)

```csharp
public static class ShopHours
{
    public const int OpenMinute  = 180;   // 9:00 AM  [KEVIN]
    public const int CloseMinute = 660;   // 5:00 PM  [KEVIN]
    public static bool IsOpen(int minuteOfDay);   // start-inclusive, end-exclusive
}

// ShopCatalog.cs also holds:
public readonly record struct ShopEntry(string ItemId, int BuyPrice);
public enum BuyResult { Ok, InsufficientFunds, NoRoom, UnknownItem }

public static class ShopCatalog
{
    public const string GeneralStore = "general_store";   // catalog id == store map id
    public static IReadOnlyDictionary<string, IReadOnlyList<ShopEntry>> All { get; }
    // general_store => [("turnip_seeds", 20), ("greenbean_seeds", 30)]   // 2x sell  [KEVIN]
    public static IReadOnlyList<ShopEntry>? TryGet(string catalogId);
}
```

ItemDef is untouched (per-shop pricing stays possible). A validation test pins every catalog
id resolving in ItemDefs.

### 3.2 WorldSim buy flow

```csharp
public BuyResult BuyItem(string itemId, int count);
```

Integrity ordering — all checks strictly before any mutation: (1) OpenShopId null or itemId
absent from `ShopCatalog.TryGet(OpenShopId)` → UnknownItem; (2) `long cost = (long)price *
count`; (3) `Money >= cost` else InsufficientFunds; (4) `inventory.HasRoomFor(itemId, count)`
else NoRoom; only then (5) `Money -= cost`; (6) `overflow = inventory.Add(...)` with a
PushError guard asserting overflow == 0; (7) fire `MoneyChanged(Money)` THEN
`InventoryChanged`. A failed buy touches nothing. NO counter selling in 3b — the shipping
bin stays the sole sell path.

### 3.3 ShopCounter (new `src/World/ShopCounter.cs`)

Area2D + IInteractable, layer 2, no sprite of its own (the counter tiles are the visual).
`PromptText => ShopHours.IsOpen(Clock.Instance.Now.MinuteOfDay) ? "Shop" : "Closed (9-5)"`
[KEVIN]; `CanInteract => GameState.Instance.PlayerHasControl`; `Interact` → if open,
`WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore)`, else no-op. Doors are never locked.

### 3.4 ShopUi (new `src/UI/ShopUi.cs`, Main.tscn UI child)

Same discipline as ChestUi (event visibility on ShopOpened/ShopClosed, `_openedFrame` guard,
Esc close → CloseShop). Money readout subscribing MoneyChanged. Rows as focus Buttons:
`"Turnip Seeds — 20g (have 3)"` (have = inventory CountOf, repaint on InventoryChanged).
Up/down navigate; interact/ui_accept/click buys 1; Shift held buys 5 (all-or-nothing per
BuyItem validation). Refusals flash "Not enough money" / "Inventory full" on the row.

### 3.5 Shopkeeper NPC

`NpcDefs` += `new NpcDef("shopkeeper", "Shopkeeper", "#b08a4a" /* [KEVIN] */, NpcSchedules.Shopkeeper)`.
`NpcSchedules.Shopkeeper`: ONE entry — `new ScheduleEntry(null, null, ShopHours.OpenMinute,
ShopHours.CloseMinute, new NpcPlacement(MapIds.GeneralStore, 6, 3, 0))` — behind the counter
facing down, absent otherwise, never on farm/town/town_hall (intro staging untouched).
Schedule bounds reference the ShopHours constants directly so "shop open" and "shopkeeper
present" can never diverge. DialogueSelector: NO change — its default null keeps the
shopkeeper silent (no Talk prompt); the sealed counter puts the NPC out of probe reach anyway.

## §4 Maps

### 4.1 Ids & registry

`MapIds` += `FarmHouse = "farm_house"`, `GeneralStore = "general_store"`; both appended to
`All`. `MapRegistry.Contains/Create` gain both. Ship ids + registry + spawns + NpcDef in the
SAME change set (NpcTests:71 and Map_RegistryCreatesAll iterate them).

### 4.2 TownMap edits

- Store facade: Wall tiles x8-14, y8-11 (clear of stone (8,6) — y6 is above the footprint —
  staging tiles, plaza, road rows 14-15, town-hall block x20-27).
- `Door { Name="StoreDoor", TargetMapId=MapIds.GeneralStore, TargetSpawnId="entry",
  Position=(11*16+8, 11*16+8) = (184,184) }` — facade gap at (11,11), town-hall pattern.
- New spawn `"from_store"` at (11,13) center (184,216).
- Storefront `Sign` at (12,12) center (200,200): `"General store. Open 9 to 5."` [KEVIN].

### 4.3 TestMap (farm) edits

- House facade: x4-9, y4-7 painted on Obstacles with a NEW blocking Wall atlas tile
  (TileCount 7→8, masonry look per TownMap), gap at the door cell (6,7).
- `Door { Name="HouseDoor", TargetMapId=MapIds.FarmHouse, TargetSpawnId="entry",
  Position=(6*16+8, 7*16+8) = (104,120) }`; (6,7) joins `_reservedTiles`.
- New spawn `"house_door"` at (6,8) center (104,136).
- StoneCoords: (5,5) RELOCATES to (5,12) (it sits inside the footprint).
- Bed node and its two reserved tiles REMOVED — (8,8)/(8,9) become plain tillable grass.
- ShippingBin (10,8) and Sign (12,8) stay, flanking the door path.

### 4.4 FarmHouseMap (new `src/World/FarmHouseMap.cs`, id "farm_house")

14x10 tiles (224x160). Plank floor + wall ring (TownHallMap pattern: south wall carries the
door). Oversized near-black ColorRect added FIRST (behind Ground, e.g. position (-640,-360)
size (1600,1000)) so the camera letterbox reads as darkness. Contents:
- `Bed` (unchanged class) at tiles (12,2)-(12,3), Position (200,56).
- Table: 2 blocking decor tiles (6,4)-(7,4) painted on Obstacles (own atlas tile, wood look).
- `Chest` at (2,2) center (40,40), StorageId `farm_house_chest`.
- `Door { TargetMapId=MapIds.Farm, TargetSpawnId="house_door" }` at (7,9) flush in the south
  wall (interiors exit via Door, not MapExit — TownHallMap precedent).
- Spawns: `"entry"` (7,8) center (120,136); `"default"` (6,5).
`_EnterTree` defaults MapId like the other maps. No farmland (IsTillable stays base false).

### 4.5 GeneralStoreMap (new `src/World/GeneralStoreMap.cs`, id "general_store")

14x10, same shell + dark surround. Contents:
- Counter: blocking tiles spanning x1-12 at y4, WALL-TO-WALL so the back area y1-3 is sealed
  (the shopkeeper is unreachable by construction).
- `ShopCounter` Area2D at (112,72) with a 192x16 RectangleShape2D spanning the counter strip.
- Shopkeeper staged at (6,3) (schedule §3.5) behind the counter.
- `Door { TargetMapId=MapIds.Town, TargetSpawnId="from_store" }` at (7,9).
- Spawns: `"entry"` (7,8); `"default"` (7,6).

## §5 Overnight report

### 5.1 Core

New `src/Core/ShippedLine.cs`: `public readonly record struct ShippedLine(string ItemId,
int Count, long Proceeds);`. `OvernightReport` becomes
`readonly record struct OvernightReport(int CropsGrown, long ShippingProceeds,
IReadOnlyList<ShippedLine>? Sales = null)` — the default param keeps existing call sites and
property reads compiling. `OvernightSim.Run` fills Sales (one line per SOLD stack) as it
empties the bin; unknown/unsellable ids stay binned and produce no line.

### 5.2 OvernightReportUi (new `src/UI/OvernightReportUi.cs`, Main.tscn UI child named "OvernightReport")

Subscribes `WorldSim.OvernightCompleted` in `_Ready` (its first subscriber; `-=` in
`_ExitTree`) and LATCHES the report — it must NOT display during the event (fires
mid-AdvanceToDayStart, screen black). API:

```csharp
public Task ShowIfPendingAsync();   // awaited by Main.RunSleepFlow
public void Dismiss();              // completes the pending wait (also used by tests)
```

ShowIfPendingAsync: if no latched report, `Sales` empty, or `ShippingProceeds <= 0` → clear
and return completed (zero-proceeds mornings show NOTHING; CropsGrown alone never
interrupts). Else show a centered card — title "Overnight Shipment" [KEVIN], one line per
ShippedLine ("Turnip x5 — 175g", display name via ItemDefs), total row "+Ng" — and complete
when `_UnhandledInput` sees `interact`/`ui_accept`/`pause` (SetInputAsHandled on all). The
pending wait also force-completes on `SaveService.AfterLoad` (a load discards the reported
world) and in `_ExitTree` (freed-Main safety); both paths hide the card.

### 5.3 Main.RunSleepFlow

`_Ready` grabs `GetNode<OvernightReportUi>("UI/OvernightReport")`. RunSleepFlow becomes:
FadeOut → AdvanceToDayStart → Save → FadeIn → `await _overnightReport.ShowIfPendingAsync()`
(inside the existing try) → finally TransitionTo(Playing). Contention resolved by
construction: the phase is still Sleeping while the card is up (player frozen, PauseMenu
inert, StoryDirector bails), and money is credited + autosaved BEFORE the card — quitting
mid-card loses only the popup, never money (accepted consequence). After dismissal the
finally restores Playing → deferred CheckTriggers → the crew beat fires immediately if the
player slept on the farm exterior, or at the house-exit travel if they slept indoors (the
normal case post-3b). The dismissing press is swallowed by `_hadControlLastPhysicsFrame`.

## §6 Help panel

- `project.godot` gains action `toggle_help` bound to physical Tab (physical_keycode
  4194306). Godot's built-in ui_focus_next keeps Tab — deliberately untouched: focused
  Controls (pause buttons, dialogue choices, chest/shop slots) consume Tab in the GUI stage
  BEFORE `_UnhandledInput`, and those contexts all lack PlayerHasControl anyway.
- New `src/UI/HelpPanel.cs` (Main.tscn UI child between StaminaBar and DialogueUi): a pure
  non-modal overlay — NO phase change, clock runs, player keeps control. Root FullRect with
  MouseFilter Ignore ALWAYS (never takes mouse or focus). Semi-transparent PanelContainer
  anchored left-center (clear of the top-right HUD and bottom hotbar) listing factual
  bindings (no lore, no [KEVIN] needed):
  Move — WASD / Arrows · Interact — E / Space · Use Tool — Left Click / C ·
  Hotbar — 1-0 / Mouse Wheel · Chest/Shop — Arrows select, E move/buy, Esc close ·
  Pause — Esc · Controls — Tab
- `_UnhandledInput`: if `!GameState.Instance.PlayerHasControl` return (leave unhandled);
  else on `toggle_help` toggle visibility + SetInputAsHandled.
- Subscribes GameState.StateChanged (`-=` in `_ExitTree`): force-hide whenever the new phase
  lacks control — it can never underlap the modal UIs. ProcessMode stays default-pausable.

## §7 Fixture v4_minimal.json (byte-frozen at ship)

Minimal v4 save exercising storage preservation: `SaveVersion: 4`, TotalMinutes 480, player
on test_farm with money 100, empty maps/bin/flags, and

```json
"Storages": {
  "farm_house_chest": { "Slots": [ { "ItemId": "turnip", "Count": 3 },
                                    { "ItemId": "future.artifact", "Count": 2 } ] },
  "future.locker":    { "Slots": [ { "ItemId": "turnip_seeds", "Count": 1 } ] }
}
```

Assertions: loads Ok; `farm_house_chest` normalized (padded) to 20 slots with both stacks
intact (unknown item preserved); `future.locker` preserved verbatim and NOT padded;
round-trips through save. Exact bytes authored by the tests agent, then frozen.

## §8 Test plan

TestRunner.MinimumExpectedTests: 63 → 85 (re-pin to the exact shipped count at integration).

### 8.1 Required modifications to existing tests

1. `Farm_ReservedTilesRefuseTilling` — vacated bed tiles (8,8)/(8,9) now ARE tillable
   (comment records the decision); facade wall tiles (e.g. (5,5),(9,4)) and door tile (6,7)
   NOT tillable; relocated stone (5,12) NOT tillable; bin (10,8)/sign (12,8) unchanged;
   (20,25) still tillable.
2. `Interaction_ProbeFindsBed` — travel to farm_house first (RequestTravel + await arrival);
   BedPosition becomes (200,56); stand at BedPosition+(0,28) facing up; same prompt assert.
3. `SleepOneNight` helper — after the day advances, if the report card is visible call its
   `Dismiss()`, then WaitUntil(Playing). All call sites compile unchanged (only the shipping
   night actually shows the card).
4. `Save_MigrationV2ToV3` (SaveTests.cs:487), `Save_MigrationChainV1ToV3` (:537),
   `Save_FixtureV3Loads` (:653) — literal `3` → `SaveMigrations.CurrentVersion` (each test's
   job is payload survival, not version pinning; precedent comment in Save_FixtureV2Loads).
5. `Map_RegistryCreatesAll` DocumentedSpawns — `[Farm] += "house_door"`; `[Town] +=
   "from_store"`; add `[FarmHouse] = {"default","entry"}`, `[GeneralStore] = {"default","entry"}`.
6. NO CHANGE (explicit): both drift guards, Integration_FullIntro,
   Integration_MeetingMissedRecovers, Integration_MainBootAndSleep, Events_MapSwapStress,
   Visual_RebuildEqualsIncremental, Npc_IntroStaging, Story_PendingBeatMatrix.

### 8.2 New tests (22)

SaveTests: `Save_MigrationV3ToV4` (v3_minimal through the real chain: version ==
CurrentVersion, Storages present+empty, full v3 payload incl. future.mystery_flag survives,
second pass byte-idempotent); `Save_MigratedStorageMatchesNewGame` (drift guard: migrated
fixture Storages equals NewGame()'s — both empty); `Save_StorageRoundTrip` (known+unknown
item ids + unknown storage key survive a save/load cycle stack-for-stack);
`Save_FixtureV4Loads` (§7 assertions); `Save_StorageRepairRules` (degenerate entries nulled,
known chest padded to 20, a 25-slot chest NOT trimmed, null storage value repaired).

StorageTests (new file): `Storage_TransferConservesItems` (inventory→chest→inventory
conserves per-id totals exactly); `Storage_PartialTransferOnFullDestination` (movable
portion moves, remainder stays in the source slot; false only when nothing moved);
`Storage_UnknownItemTransfers` (unknown id both directions at maxstack 1, never destroyed);
`Storage_TransferFiresEvents` (exactly one StorageChanged(id) + one InventoryChanged per
successful transfer, NONE on refusal); `Menu_SessionGates` (OpenStorage/OpenShop refused
without PlayerHasControl and while another session is open; open → Menu, ClockRuns false,
tree NOT paused; Close → Playing; AfterLoad mid-session force-closes and restores Playing).

EconTests (new file): `Shop_BuyHappyPath` (Ok: exact debit, stack added, MoneyChanged(new
balance) then InventoryChanged); `Shop_BuyFailuresMutateNothing` (all three failure results
leave money, inventory, events untouched); `Shop_CatalogIdsResolve` (every catalog id
resolves in ItemDefs with BuyPrice > 0); `Shop_HoursBoundary` (IsOpen false@179, true@180,
true@659, false@660; shopkeeper schedule bounds equal the ShopHours constants).

NpcTests: `Npc_ShopkeeperSchedule` (Resolve → (general_store,6,3,0) during 180-659, null at
179/660, invariant under all intro-flag combinations, never on farm/town/town_hall).

FarmTests: `Overnight_ReportItemizesSales` (Sales lines match sold stacks and sum to
ShippingProceeds; unknown ids stay binned with no line).

StoryTests: `Story_CrewBeatNotPendingIndoors` (pure IntroRules: RoadCleared set →
PendingBeat null for "farm_house"/"general_store", CrewArrival for "test_farm").

TravelTests: `Travel_InteriorRoundTrips` (Main boot: farm Door → farm_house "entry" →
interior Door → "house_door"; town Door → general_store → "from_store"; Player.MapId and
spawn positions exact).

IntegrationTests: `Integration_ChestOpenTransferClose` (Main boot, travel to farm_house,
open the chest via interact without the opening press transferring slot 0, transfer both
ways, Esc closes to Playing, the closing frame neither re-opens nor swings a tool — the
closing-press regression pin; drive input via Input.ParseInputEvent press/release pairs, or
call the UI handlers directly with constructed events if headless dispatch proves flaky —
the assertions are the contract); `Integration_MorningReportFlow` (deposit a turnip, sleep:
card visible while the phase is still Sleeping, correct line/total, autosave already on
disk; Dismiss() → Playing; a second empty-bin sleep reaches Playing with no card);
`Integration_SleepInHouseCrewBeatOnExit` (plant, enter the house, sleep on the crew morning:
no beat indoors; walk out the door; crew beat fires and completes — dialogue-driven, NOT
pre-stamped); `Help_ToggleGating` (toggle_help shows/hides in Playing; inert in
Dialogue/Menu/Paused; force-hides on control loss).

## §9 [KEVIN] ledger

1. Buy prices: turnip_seeds 20g, greenbean_seeds 30g (2x sell). Seeds-only catalog for now.
2. Store hours 9-5; "Closed (9-5)" prompt; storefront sign copy; store NAME not invented.
3. Shopkeeper: role label, tunic #b08a4a; silent this phase (counter seals them off) — say
   the word for a greeting line or town/meeting appearances.
4. Chest capacity 20; whole-stack transfers (no split/quantity picker).
5. Report copy ("Overnight Shipment", "Turnip x5 — 175g"); skip-when-zero; no crops-grew
   line; quit-mid-card loses only the popup (money is already autosaved).
6. New games still spawn in the field at (20,15) — day-0-only (you wake where you slept from
   night one); kept for test stability + "morning after the storm" framing.
7. Building placements pending art direction (store x8-14,y8-11; house x4-9,y4-7; 14x10
   interiors); farm stone (5,5)→(5,12).
8. Meeting hour, dialogue copy, names — carried over from the Phase 3 ledger, still open.

## §10 Deferred ledger

Counter selling (shipping bin remains the sole sell path); shopkeeper dialogue; stack
splitting / quantity picker; additional chests (model already supports arbitrary storage
ids); locked/hours-gated doors; day-0 in-bed start; store name; catalog beyond seeds;
test_farm→farm rename (carried).

## §9b As-built addenda (integration + adversarial review, 2026-08-25)

Shipped: 87 headless tests green (85 per §8 + 2 review pins), zero build warnings.
`docs/foundation-spec.md` carries the Phase-enum amendment (`Menu` appended; both derived
queries false there; tree pause untouched).

Accepted implementation deviations (agent-documented, integrator-ratified):
- ShopUi's refusal flash renders in a shared label under the row list (appending to the
  focused row would resize it mid-interaction).
- ChestUi sizes its grid from the model, so an over-capacity save renders extra rows
  (honors never-trim). The panel is wider than the 14x10 interiors, so its edges float
  over the dark surround — cosmetic, placeholder-art era.
- Travel_InteriorRoundTrips asserts interior "entry" arrivals within 3px (the spawn tile
  abuts the door blocker; depenetration can nudge the 12x8 feet box).
- Integration_ChestOpenTransferClose drives chest→inventory by injected E press but
  inventory→chest through the model op (headless grid-focus navigation is unreliable).
- BuyItem carries a defensive `count <= 0` refusal ahead of the §3.2 ordering.
- Interior surround ColorRects set MouseFilter.Ignore (default Stop would eat clicks).

Adversarial review (4 finders → per-finding xhigh skeptics; 6 distinct confirmed, 1
refuted) — all confirmed items fixed:
1. (low) Bin-merge int overflow could mint a negative count that the overnight sale turns
   into negative money, which the next load's validator quarantines. Fixed: the merge is
   long-checked and refuses non-destructively; the sale loop skips `Count <= 0` entries.
   Pinned by Econ_DepositOverflowRefused + Overnight_NonPositiveBinEntryNeverSells.
2. (medium) Double-tapping E on the shipment card re-slept immediately (the player is
   necessarily at the bed; the controller guard swallows only the first frame). Fixed:
   RunSleepFlow holds Sleeping for a 0.3 s mash-grace after a card was actually shown;
   zero-proceeds mornings keep exact prior timing.
3. (medium) The "[E] Shop"/"[E] Closed (9-5)" prompt went stale across the 9:00/17:00
   boundary while focus was held. Fixed: InteractionPrompt re-derives from focus each
   MinuteTicked (display-only event, Hud precedent) with a freed-node validity guard.
4. (medium) A pre-3b save position inside the new building facades loaded embedded in
   collision. Fixed: MapRoot.IsStandable (walkable ground, no obstacle, no Door tile) +
   a boot guard in Main.LoadMap that bounces invalid positions to the spawn with a
   warning — the save file itself is never rewritten.
5. (low, ACCEPTED not fixed) Pre-3b tile records inside the farmhouse footprint x4-9,y4-7
   stay in the model (preservation rule) but render under the facade and are unreachable.
   Kevin's real save was scanned: zero affected tiles. A relocation repair was judged not
   worth its fixture churn; revisit only if real saves surface with tiles there (§9 item).
6. (low) The §9b/foundation-spec/CLAUDE.md doc obligations — this section closes them.

Dev flags added for visual verification: `--add-minutes <n>` (advances the clock through
Clock.AdvanceMinutes, in-memory only) and `--open-ui <chest|shop|help>` (pops a 3b UI
after boot). Both live in Main.HandleCmdlineArgs beside `--start-map`.
