# src/Systems — the four autoloads

The four autoloads, in registration order (project.godot; each may depend only on
earlier ones): **GameState** (top-level phase enum + the derived gates ClockRuns /
PlayerHasControl / CanStartDialogue — Playing or Cutscene), **Clock** (drives the pure
`ClockModel` from `_Process`, re-exposes its events 1:1), **SaveService** (owns the
`GameData` graph and the save/load pipeline — atomic tmp+rename writes, quarantine of
unreadable files, load-time repair/migration), **WorldSim** (the single
gameplay-mutation bus — all model writes flow through it, incl. story flags, travel
requests, the dialogue session, chest/shop Menu sessions, transfers, purchases, and
scooter mount/park; UI subscribes to ITS events, never to events on Core objects —
SaveService swaps `Current` wholesale on load).

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
  3 UI events, 4 NPC + scooter sync, 5 StoryFlagSet — all before Main's autosave.
  Violating it is a bug.
- `OvernightCompleted` fires mid-advance while the screen is black — subscribers latch,
  never display, in the handler (the report card is shown later, by Main's sleep flow).

## Flags, sessions, transfers, scooter

- All flag writes go through `WorldSim.SetStoryFlag` (only-if-absent; on a NEW set:
  repaint every registered map, resync NPCs, fire `StoryFlagSet`). The one exception is
  internal: OnDayStarted's dawn batch writes via `TrySetFlag` directly so the
  repaint/sync/events land once, in the ordering above.
- Chest/shop UIs run in the Menu phase, owned by WorldSim's Open*/Close* sessions
  (OpenStorageId/OpenShopId) — UIs never call TransitionTo themselves; Menu freezes
  clock and player but never tree pause.
- Transfers/purchases (TransferToStorage/TransferToInventory/BuyItem) check strictly
  before mutations; unknown storage keys and item ids are preserved verbatim.
- The scooter's write path is here: MountScooter / DismountScooter / ParkScooterAt —
  `GameData.Scooter` is either parked or mounted, never both. Never ridden — or
  parked — indoors: Main's travel flow auto-parks at the door, and SaveService's load
  repair re-parks impossible interior states home via `MapIds.IsInterior` (a table with
  a drift guard against each map's IsInterior). Mounting has NO ceremony (texture swap
  + speed, no fade). Design intent and the view side live in src/World/CLAUDE.md.
- A load can land mid-session: WorldSim's AfterLoad handler discards any open dialogue /
  chest / shop session WITHOUT applying its flags, gives the phase back, and resyncs
  NPCs + scooter. Loads never clobber `SaveService.Current` on failure.
