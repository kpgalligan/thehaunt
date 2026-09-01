# src/Systems — the four autoloads

The four autoloads, in registration order (project.godot; each may depend only on
earlier ones): **GameState** (top-level phase enum + the derived gates ClockRuns /
PlayerHasControl / CanStartDialogue — Playing or Cutscene), **Clock** (drives the pure
`ClockModel` from `_Process`, re-exposes its events 1:1), **SaveService** (owns the
`GameData` graph and the save/load pipeline — atomic tmp+rename writes, quarantine of
unreadable files, load-time repair/migration; NewGame rolls the save's one RNG
`Seed` via GD.Randi), **WorldSim** (the single
gameplay-mutation bus — all model writes flow through it, incl. story flags, travel
requests, the dialogue session, chest/shop/mailbox/garage-sale Menu sessions,
transfers, purchases (BuyItem, and the one-time BuyGarage), letter reads + package
takes (ReadLetter/TakeLetterItems), scooter mount/park, the garage operation
(the hourly customer roll + WorkOnGarageJob), and skill XP grants (private
GrantSkillXp — XP sources are bus observations of outcomes, never UI calls); UI
subscribes to ITS events, never to events on Core objects — SaveService swaps
`Current` wholesale on load).

## The garage operation (Kevin, 2026-08-30)

- WorldSim is the ONE `HourTicked` subscriber (src/CLAUDE.md): each open hour
  (GarageOpsRules.IsOpenHour, [9,18)) with the deed stamped and a free lift rolls
  `GarageOpsRules.CustomerRoll(data.Seed, day, hour)` — a pure hash, so a re-fired
  tick is idempotent (the (ArrivalDay, ArrivalHour) stamp is the guard). Order on
  an arrival: mutate -> repaint -> `GarageJobsChanged` -> `GarageCustomerArrived`.
  Live play only: AdvanceToDayStart fires no hour ticks, so slept-through open
  hours roll nothing (deliberate v1 limitation; the stateless hash makes a dawn
  catch-up addable without reshuffling schedules).
- `WorkOnGarageJob(lift)`: GarageOpsRules.DoWork mutates (checks strictly first,
  refusals fire NO events), then repaint -> `StaminaChanged` -> `GarageJobsChanged`
  -> on completion `SkillsChanged` -> `SkillLeveledUp?` -> `GarageJobCompleted`.
  The bus observes CompletedJob for the mechanical-repair point, exactly as it
  observes Harvested for the farming point (the FirstPlanting pattern).
- Dawn resolution lives in OvernightSim's garage step (payment BEFORE expiry — a
  pinned invariant; see src/Core/CLAUDE.md) and reaches UI through the
  OvernightReport's Garage lines; OnDayStarted's step 3 also fires
  `GarageJobsChanged` unconditionally, so the event's contract stays "fires on
  every jobs-list change".

Menu sessions are MUTUALLY exclusive and the exclusion is pairwise: every Open*
gate names all four session slots (OpenStorageId, OpenShopId, MailboxOpen,
GarageSaleOpen), and adding a fifth session means touching every existing gate plus
the AfterLoad discard block. The bus also observes farm outcomes for story stamps: Planted ->
FirstPlanting, and Watered ON A TILE HOLDING A CROP -> FirstWatering (the
first-crops quest's completion; watering empty tilled soil never stamps).

The subscriber-side time/phase rules (MinuteTicked vs TenMinuteTicked, ClockRuns
gating, GetTree().Paused ownership) are in src/CLAUDE.md.

## The day boundary

- `Clock.AdvanceToDayStart()` is the only day-boundary crossing (the clock clamps at
  1:59 AM — `ClockModel.AtEndOfDay`); it fires no minute/ten-minute/hour ticks — only
  DayEnded then DayStarted.
- OvernightSim mutates the model on DayEnded (payload day — the day being closed);
  WorldSim repaints maps and fires UI events on DayStarted. Both fire synchronously
  inside AdvanceToDayStart, before Main's autosave.
- OnDayStarted's committed ordering (spec §1.4, risk R3): 1 dawn flags, 2 repaint,
  3 UI events (OvernightCompleted, MoneyChanged, StaminaChanged, InventoryChanged,
  GarageJobsChanged), 4 NPC + scooter sync, 5 StoryFlagSet — all before Main's
  autosave. Violating it is a bug.
- `OvernightCompleted` fires mid-advance while the screen is black — subscribers latch,
  never display, in the handler (the report card is shown later, by Main's sleep flow).

## Field obstacles

- `WorldSim.EnsureObstacles(map)` is the one generation trigger: Main calls it between
  AddChild and ApplyState on both map-load paths. Once per map per save
  (`MapState.ObstaclesSeeded`); candidates come from the map view
  (`MapRoot.ObstacleCandidates`); the player's footing ring, the parked scooter, and
  every NPC staging slot the map's schedules can ever host are excluded model-side, and the SEED is rolled here (`GD.Randi`) — Core's ObstacleGen
  stays deterministic under an explicit seed. Tool strikes flow through
  `UseSelectedItem` like everything else: Struck/Felled/Broken refresh the view via
  `map.RefreshObstacle`, and the finals fire InventoryChanged for the yield.

## Flags, sessions, transfers, scooter

- All flag writes go through `WorldSim.SetStoryFlag` (only-if-absent; on a NEW set:
  repaint every registered map, resync NPCs, fire `StoryFlagSet`). The one exception is
  internal: OnDayStarted's dawn batch writes via `TrySetFlag` directly so the
  repaint/sync/events land once, in the ordering above.
- Chest/shop/mailbox/garage-sale UIs run in the Menu phase, owned by WorldSim's
  Open*/Close* sessions (OpenStorageId/OpenShopId/MailboxOpen/GarageSaleOpen) — UIs
  never call TransitionTo themselves; Menu freezes clock and player but never tree
  pause. OpenGarageSale additionally refuses once garage.deed is stamped — a sold
  garage has nothing left to sell.
- Transfers/purchases (TransferToStorage/TransferToInventory/BuyItem, and BuyGarage —
  GarageRules validates, the bus debits, stamps the deed via SetStoryFlag, fires
  MoneyChanged, then closes its own session) check strictly before mutations; unknown
  storage keys and item ids are preserved verbatim.
- The scooter's write path is here: MountScooter / DismountScooter / ParkScooterAt —
  `GameData.Scooter` is either parked or mounted, never both. Never ridden — or
  parked — indoors: Main's travel flow auto-parks at the door, and SaveService's load
  repair re-parks impossible interior states home via `MapIds.IsInterior` (a table with
  a drift guard against each map's IsInterior). Mounting has NO ceremony (texture swap
  + speed, no fade). Design intent and the view side live in src/World/CLAUDE.md.
- A load can land mid-session: WorldSim's AfterLoad handler discards any open dialogue /
  chest / shop / mailbox / garage-sale session WITHOUT applying its flags, gives the
  phase back, and resyncs NPCs + scooter. Loads never clobber `SaveService.Current` on failure.
