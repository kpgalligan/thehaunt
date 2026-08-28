# src/Core — the pure model layer

PURE C# — no `using Godot`, test-enforced (SourceRulesTests); this is what keeps the
model testable without a scene tree. What lives here: GameTime/calendar, ClockModel,
GameData + save DTOs (PlayerData/MapState/TileRecord/ItemStackRecord/PlacedObjectRecord,
SaveJsonContext), migrations (SaveMigrations.CurrentVersion = 5), item/crop defs (code
registries ItemDefs/CropDefs), InventoryData, FarmActions, OvernightSim + ShippedLine;
storage: StackOps/StorageData/StorageIds; scooter: ScooterData/ScooterRules;
WorkAnimation (the tool work loop's pure timing/interruption contract — the tile
mutation fires on ENTRY to its impact frame, never at press time); shop:
ShopCatalog (+ ShopEntry/BuyResult)/ShopHours; story: StoryKeys/IntroRules/BarnRules/
MotelRules/MapIds/RoadWrap, DialogueDef(s)/DialogueSession/DialogueSelector,
NpcDef(s)/NpcSchedules.

## Rules (violations are bugs)

- Time-dependent saved state stores day-indexes (`LastWateredDay`), never booleans.
- Unknown item/crop ids from save files are ALWAYS preserved, never destroyed (renders
  as '?' placeholder). An id rename ships as a save migration.
- Migrations are frozen JSON literals, only-if-absent, and never call live code
  (ItemDefs/StarterKit/NewGame). A drift guard failing (Save_MigratedKitMatchesNewGame,
  Save_MigratedStoryMatchesNewGame) means: make a conscious decision — never edit a
  frozen migration.
- Story flags are monotone day-stamped entries in GameData.StoryFlags (no unset; absence
  = false; unknown keys preserved). All flag writes go through WorldSim.SetStoryFlag
  (WorldSim's internal dawn batch is the one documented exception — src/Systems/CLAUDE.md).
  A flag's VALUE is the day it was stamped, never a level — which is why the barn's
  three drawn states are two monotone flags (`BarnRules`, never an int in one flag) and
  motel occupancy is a derivation (`MotelRules.OccupiedRooms`), never an int. Nothing
  advances the barn flags yet; that seam is deliberately empty.
- Story-flag ids live as constants in `StoryKeys` — a validation test enforces that
  every flag referenced by dialogue defs resolves to a constant there.
- Storage containers live in GameData.Storages (id -> StorageData); unknown storage
  keys and item ids are preserved verbatim; transfers/purchases go through WorldSim
  (TransferToStorage/TransferToInventory/BuyItem — checks strictly before mutations).

## Orientation facts (from the code)

- ItemDefs/CropDefs: insertion order is the canonical iteration order for `All` — the
  Crops atlas assigns one row per CropDef in that order. `Get` throws on a missing id
  (a code bug); `TryGet` is the null-tolerant lookup for ids coming from save files.
- NpcDef: `SpriteSheet` is a res:// path to a cast sheet (cast-sprites handoff, four
  atlases under `assets/sprites/cast/`), `SpriteBlock` the character's 96px-wide block
  index inside it; `Schedule` is FIRST match wins. `NpcPlacement.Ambit` (tiles,
  Chebyshev; 0 = a fixture) is VIEW-side flavour only — the model's answer to "where is
  this NPC" is always the staging tile.
- OvernightSim mutates the model on DayEnded (payload day) — Main owns the sleep flow.
  It also parks the scooter home (farmhouse frontage): never stolen, never lost.
