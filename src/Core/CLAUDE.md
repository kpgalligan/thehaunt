# src/Core — the pure model layer

PURE C# — no `using Godot`, test-enforced (SourceRulesTests); this is what keeps the
model testable without a scene tree. What lives here: GameTime/calendar, ClockModel,
GameData + save DTOs (PlayerData/MapState/TileRecord/ItemStackRecord/PlacedObjectRecord,
SaveJsonContext), migrations (SaveMigrations.CurrentVersion = 6), item/crop/obstacle defs (code
registries ItemDefs/CropDefs/ObstacleDefs), InventoryData, FarmActions, ObstacleGen
(seeded one-shot field generation — WorldSim owns the trigger and the randomness),
OvernightSim + ShippedLine;
storage: StackOps/StorageData/StorageIds; scooter: ScooterData/ScooterRules;
mail: LetterDef(s)/MailRules/MailActions; quests: QuestDef(s)/QuestRules;
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
- Field obstacles (tree/stump/rock) are PlacedObjectRecords in MapState.Objects —
  a SHARED seam: an ObjectId ObstacleDefs does not know is preserved untouched, never
  struck, never destroyed. The record's (X, Y) is its one solid cell (a tree's trunk);
  FarmActions' obstacle branch owns the whole cell (the matching tool strikes, hits
  accumulate in HitsTaken, the FINAL hit pays the yield all-or-nothing — harvest
  precedent — and everything else refuses, so nothing can till under a standing
  obstacle whatever the view said). `MapState.ObstaclesSeeded` means "ObstacleGen has
  run here", which is NOT "Objects is empty" — a cleared field stays cleared, and an
  old save generates on its next visit.
- Mail and quests keep NO model state of their own — both are pure derivations over
  StoryFlags (MailRules/QuestRules), so they needed no save-version bump. A letter is
  delivered by monotone conditions (never vanishes), read under its ReadFlag, and a
  package pays out once under its TakenFlag (MailActions owns the all-or-nothing
  joint room check; WorldSim stamps the flag). A quest is a named window between its
  StartFlag and CompleteFlag; "born completed" (world event before hand-out) is a
  legal order QuestRules.CompletedBy reports on the late start stamp.
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
