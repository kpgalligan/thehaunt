# Phase 3 Spec — The Scripted Intro (story flags, maps & travel, dialogue, NPCs)

Authoritative contract for Phase 3. Produced by the three-lens design panel + judge
(2026-08-25), verified against the working tree. Read `docs/foundation-spec.md` and
`docs/phase2-spec.md` first — every rule there stays in force. Save schema goes to **v3**.
No new autoload: WorldSim remains the single gameplay-mutation bus; story orchestration is
a plain Node owned by Main.

Scope: ship the game's actual opening as a playable slice — road blocked by the storm →
first planting → road clears at dawn → repair-crew beat on the farm → travel to town →
town-hall meeting beat at night where the mayor explains the curse.

`[KEVIN]` marks placeholder content (copy, staging, times) that only restates canon and
awaits user review. Inventing lore beyond canon restatement is forbidden; NPC names are
forbidden — role labels only.

Verified engine/codebase facts this design leans on (do not re-litigate):

- `GameState.StateChanged` dispatches synchronously inside `TransitionTo`; handlers that
  call `TransitionTo` nest dispatches. Therefore: **beats start only via `CallDeferred`**.
- `ClockModel.Accumulate` does not re-check `ClockRuns` mid-loop (up to 4 more minutes can
  tick after a synchronous phase change inside `TenMinuteTicked`). Deferred checks absorb this.
- `Main.RunSleepFlow` runs `AdvanceToDayStart()` then `Save()` — anything stamped during
  DayEnded/DayStarted lands in the morning autosave. Load-bearing for beat replay.
- `Bed.CanInteract` requires Playing; PauseMenu is inert outside Playing/Paused — no save
  path exists mid-beat/mid-dialogue. Any future save path must re-prove this invariant.
- `SaveService.BeforeSave` / `AfterLoad` events exist. `SaveJsonContext` is a single
  `[JsonSerializable(typeof(GameData))]` graph walk — new `GameData` fields need no edit.
- `ScreenFade.FadeOut/FadeIn(seconds = 0.4)` accept a duration; `FadeOut(0.25)` is valid.
- `_UnhandledInput` runs before the same frame's `_PhysicsProcess`.

---

## §1 Story state

### 1.1 GameData (v3) — exactly one new field plus helpers

```csharp
public Dictionary<string, long> StoryFlags { get; set; } = new();   // flag id -> DayIndex stamped

public bool HasFlag(string id) => StoryFlags.ContainsKey(id);
public long FlagDay(string id) => StoryFlags.TryGetValue(id, out var d) ? d : -1;
public bool TrySetFlag(string id, long day);   // only-if-absent; true iff newly set
```

Flags are **monotone** — no unset API, absence = false, stamp = day-index (never a bool).
Unknown keys from saves round-trip untouched (the preserve-unknown-ids rule applied to flags).

### 1.2 `src/Core/StoryKeys.cs` — the only legal flag ids in code

```csharp
public static class StoryKeys
{
    public const string FirstPlanting   = "intro.first_planting";
    public const string RoadCleared     = "intro.road_cleared";
    public const string CrewArrivalDone = "intro.crew_arrival_done";
    public const string MeetingDone     = "intro.meeting_done";
}
```

A validation test enforces that every flag referenced by dialogue defs resolves to a
StoryKeys constant.

### 1.3 `src/Core/IntroRules.cs` — pure, total, headless-testable

```csharp
public enum StoryBeatId { CrewArrival, TownMeeting }

public static class IntroRules
{
    public const int MeetingStartMinuteOfDay = 720;   // 6:00 PM  [KEVIN]

    // [RoadCleared] iff HasFlag(FirstPlanting) && newDayIndex > FlagDay(FirstPlanting)
    // && !HasFlag(RoadCleared); else empty. Evaluated at EVERY dawn — idempotent.
    public static IReadOnlyList<string> FlagsToSetOnDayStarted(GameData data, long newDayIndex);

    // CrewArrival: HasFlag(RoadCleared) && !HasFlag(CrewArrivalDone) && activeMapId == MapIds.Farm
    // TownMeeting: HasFlag(CrewArrivalDone) && !HasFlag(MeetingDone)
    //              && activeMapId == MapIds.TownHall && now.MinuteOfDay >= MeetingStartMinuteOfDay
    // CrewArrival wins if both pend (hostile save). NO day-equality term anywhere.
    public static StoryBeatId? PendingBeat(GameData data, GameTime now, string activeMapId);
}
```

Both functions are **total**: any flag combination (including hostile hand-edited saves)
degrades to skip-or-replay, never throws.

Trigger semantics (exact):

| Event | Trigger |
|---|---|
| `intro.first_planting` | WorldSim, on the first `ActionOutcome.Planted` (no `FarmActions` change — the bus observes the outcome). Stamped `Clock.Now.DayIndex`. Post-midnight planting stamps the ending day → road clears after the upcoming sleep. Intended. |
| `intro.road_cleared` | Every dawn via `FlagsToSetOnDayStarted`. No planting ⇒ road stays blocked forever (no timer). Plant day N ⇒ stamped at dawn of day N+1, in the autosave. |
| Crew beat | First deferred check after phase returns to Playing post-sleep, on the farm. Completion flag stamped only by the dialogue's terminal node — quit mid-beat replays from the morning autosave. |
| Meeting beat | Deferred check after travel ends, or `TenMinuteTicked` while standing in the hall at/after 18:00. **No missed state**: no day term, so it re-pends every evening from 18:00 until attended. The clamp edge (entering after the day's last tick at 1190) is covered by the travel-end `StateChanged` path. |

### 1.4 WorldSim — flag writes (all model writes stay on the bus)

```csharp
public event Action<string, long>? StoryFlagSet;   // fired only on NEW sets: (flagId, dayStamped)
public bool SetStoryFlag(string flagId);           // TrySetFlag(Clock.Now.DayIndex); on success:
                                                   // repaint registered maps (ApplyState), SyncNpcsNow(), fire event
```

- `UseSelectedItem`: on `ActionOutcome.Planted` → `SetStoryFlag(StoryKeys.FirstPlanting)`.
- `OnDayStarted` **committed ordering** (violating it is a bug — risk R3):
  1. apply `IntroRules.FlagsToSetOnDayStarted` via `TrySetFlag` directly (collect newly set);
  2. the existing full `ApplyState` repaint loop (now also paints the road, §6);
  3. the existing UI events (`OvernightCompleted`, `MoneyChanged`, `StaminaChanged`, `InventoryChanged`);
  4. `SyncNpcsNow()`;
  5. fire `StoryFlagSet` per newly-set flag.

  All synchronously inside `AdvanceToDayStart` — i.e. **before Main's autosave**. Story
  rules do NOT go into `OvernightSim` (the farming sim stays story-free).

### 1.5 `src/Story/StoryDirector.cs` — plain Node, child of Main (NOT an autoload)

Rationale (frozen): it dies with Main, so headless tests that don't boot Main never
evaluate beats; Main already owns scene flows; the four-autoload roster is unchanged.
It never writes the model — flags go through `WorldSim.SetStoryFlag` / dialogue terminals.

Subscriptions (`+=` in `_Ready`, `-=` in `_ExitTree`): `GameState.StateChanged`,
`Clock.TenMinuteTicked`, `SaveService.AfterLoad`, `WorldSim.StoryFlagSet`,
`WorldSim.DialogueFinished`. **Every trigger source funnels through
`CallDeferred(nameof(CheckTriggers))`** — never a synchronous beat start (see verified
facts). See §5 for the beat coroutine.

---

## §2 Maps & travel

### 2.1 `src/Core/MapIds.cs` (pure — referenced by IntroRules/NpcSchedules/tests)

```csharp
public static class MapIds
{
    public const string Farm = "test_farm";   // rename to "farm" deferred to the first editor-authored map (its own migration)
    public const string Town = "town";
    public const string TownHall = "town_hall";
    public static readonly IReadOnlyList<string> All = new[] { Farm, Town, TownHall };
}
```

### 2.2 `src/World/MapRegistry.cs`

```csharp
public static bool Contains(string mapId);
public static MapRoot Create(string mapId);   // Farm → new TestMap(), Town → new TownMap(), TownHall → new TownHallMap()
```

A factory later becomes `PackedScene.Instantiate` with zero call-site change. Unknown id
from a save: `Main.LoadMap` guards — `GD.PushError`, fall back to `MapIds.Farm` with
`HasPosition = false`; the unknown map's `MapState` **stays untouched** in `GameData.Maps`
(preservation rule).

### 2.3 WorldSim — travel bus

```csharp
public event Action<string, string>? TravelRequested;      // (mapId, spawnId); Main subscribes once
public bool RequestTravel(string mapId, string spawnId);   // gate: PlayerHasControl && MapRegistry.Contains; fire; true
public void CompleteTravel(string mapId);                  // Current.Player.MapId = mapId; SyncNpcsNow();
public bool IsMapActive(string mapId);                     // FindRegisteredMap(mapId) != null (made public)
```

### 2.4 Main — travel flow (Main owns fades/swaps, like the sleep flow)

```csharp
private bool _travelRunning;
private async Task RunTravel(string mapId, string spawnId)
{
    _travelRunning = true;
    GameState.Instance.TransitionTo(GameState.Phase.Cutscene);   // clock + player frozen; tree NOT paused
    try
    {
        await _fade.FadeOut(0.25);
        _currentMap?.QueueFree();               // FindRegisteredMap's IsQueuedForDeletion guard covers the same-frame window
        var map = MapRegistry.Create(mapId);
        _currentMap = map;
        _mapHost.AddChild(map);
        map.ApplyState(SaveService.Instance.Current.GetMap(map.MapId));
        WorldSim.Instance.CompleteTravel(map.MapId);             // model write via the bus + NPC sync
        _player.GlobalPosition = map.GetSpawn(spawnId);          // node-owned volatile state, set while black
        _player.ApplyCameraLimits(map.GetCameraLimits());
        await _fade.FadeIn(0.25);
    }
    catch (Exception e) { GD.PushError($"Travel failed: {e}"); }
    finally { _travelRunning = false; GameState.Instance.TransitionTo(GameState.Phase.Playing); }
}
```

- `OnTravelRequested` refuses while `_travelRunning`.
- `LoadMap` becomes the registry-backed boot wrapper (no fade, no phase change).
- The `finally`'s `TransitionTo(Playing)` triggers StoryDirector's deferred check —
  arriving in the town hall inside the meeting window chains straight into the beat. The
  one-frame Playing window before the beat is accepted.
- **No autosave on travel** — sleep remains the only autosave. Quitting mid-travel loses
  at most progress since last sleep.
- **Ordering note (load-bearing)**: `MapExit.BodyEntered` fires during the physics flush;
  Main's handler must hit its first `await` before any tree mutation (the code above does).

### 2.5 Triggers

`src/World/MapExit.cs` — `Area2D` walk-on. Collision layer 0, mask 1, monitoring on.
Godot `BodyEntered` signal (auto-disconnects on free):

```csharp
public string TargetMapId { get; set; } = ""; public string TargetSpawnId { get; set; } = "default";
public Func<bool>? IsEnabled;   // set by the owning map; null = always enabled
// handler: if (body is PlayerController && (IsEnabled?.Invoke() ?? true)
//              && GameState.Instance.PlayerHasControl)
//              WorldSim.Instance.RequestTravel(TargetMapId, TargetSpawnId);
```

`src/World/Door.cs` — `Area2D, IInteractable`, pattern-copied from Sign: `PromptText =>
"Enter"`, `CanInteract => GameState.Instance.PlayerHasControl && !IsQueuedForDeletion()`,
`Interact => WorldSim.Instance.RequestTravel(TargetMapId, TargetSpawnId)`. Layer 2, mask 0,
monitorable.

### 2.6 Spawns (existing `GetSpawn(id)` contract — Marker2Ds under `Spawns/`)

- farm: `default` (existing), `road` — west of the blockade line, **≥1 tile clear of the exit area**
- town: `from_farm`, `from_hall`
- town_hall: `entry`

**Authoring rule, asserted by test: arrival spawns sit at least one tile outside any
MapExit area.** (Belt: the `PlayerHasControl` gate refuses the arrival-frame `BodyEntered`
during Cutscene and it never re-fires without exit + re-entry.)

### 2.7 Camera limits

`MapRoot.GetCameraLimits()` base implementation expands the computed rect to **at least
640×360, centered** (an interior smaller than the viewport makes Camera2D limits
unsatisfiable and pins the view asymmetrically). `TownHallMap` is additionally authored
≥ 40×23 tiles so the clamp never engages in practice.

### 2.8 New maps (programmatic placeholder art, TestMap pattern)

- `src/World/TownMap.cs` (48×30): grass base; east–west dirt road along rows y=14–15
  (continuous with the farm road rows); west-edge `MapExit` covering tiles (0,14),(0,15) →
  `test_farm`/`road` (**always enabled — leaving town is never gated**); spawn `from_farm`
  at (2,15); stone town-hall facade block around x=20–27, y=6–11 with a `Door` at (23,11)
  → `town_hall`/`entry` and spawn `from_hall` at (23,13); an open town-square plaza south
  of the road around (22–26, 18–21); blocking edges elsewhere. The frozen staging tiles
  (§4.3: (24,19), (30,13), (31,16), (33,13)) and all spawn/door-approach tiles must be
  walkable and obstacle-free.
- `src/World/TownHallMap.cs` (40×23): floor, blocking wall ring, podium visual around
  (19,4)–(21,5), `Door` at (20,21) → `town`/`from_hall`, spawn `entry` at (20,19). The
  frozen staging tiles (§4.3: (20,6), (18,12), (20,12), (22,12)) must be walkable floor.
- No bed outside the farm — day advance stays farm-anchored.

---

## §3 Dialogue

### 3.1 Data model — `src/Core/DialogueDef.cs`

```csharp
public sealed record DialogueLine(string SpeakerRole, string Text);           // "" = narration
public sealed record DialogueChoice(string Text, string NextNodeId, string? SetsFlag = null);
public sealed record DialogueNode(
    string Id,
    IReadOnlyList<DialogueLine> Lines,                // never empty (test-enforced)
    string? NextNodeId = null,                        // linear continuation
    IReadOnlyList<DialogueChoice>? Choices = null,    // shown with the LAST line; exclusive with NextNodeId (test-enforced)
    string? SetsFlag = null);                         // accumulated on node entry, applied at session end
public sealed record DialogueDef(string Id, string StartNodeId, IReadOnlyDictionary<string, DialogueNode> Nodes);
```

### 3.2 `src/Core/DialogueDefs.cs` — registry (`All` / `Get` / `TryGet`), catalog:

| Id | Content (all copy `[KEVIN]`, canon restatement only) |
|---|---|
| `intro_crew_arrival` | Foreman-led scene; crew surprised **in a bad way** to find a new owner; player explains the purchase; one converging 2-way choice on how the player responds; ends with "attend the town hall meeting tonight". **Every terminal path's node carries `SetsFlag = StoryKeys.CrewArrivalDone`** (test-enforced, DFS). |
| `intro_town_meeting` | Mayor; hub-and-spoke Q&A — "Why can't I leave?" / "Can I sell?" / "What tribute?" — each spoke a strict canon restatement returning to the hub, plus an exit choice. Every terminal path sets `StoryKeys.MeetingDone` (re-raising on hub re-entry is harmless — only-if-absent). |
| `foreman_wait` | "Meeting's tonight" holding line. |
| `foreman_after`, `mayor_after` | Post-meeting lines. |
| `crew_worker_default` | Ambient worker line. |

No cancel input — sessions run to completion (keeps the beat machine two-state).

### 3.3 `src/Core/DialogueSession.cs` — pure state machine, never serialized

```csharp
public sealed class DialogueSession
{
    public DialogueSession(DialogueDef def);          // enters StartNodeId (accumulating its SetsFlag)
    public DialogueDef Def { get; }
    public DialogueLine CurrentLine { get; }
    public bool AtChoices { get; }                    // last line of a Choices node, not Finished
    public IReadOnlyList<DialogueChoice> CurrentChoices { get; }   // empty unless AtChoices
    public bool Finished { get; }
    public IReadOnlyList<string> FlagsRaised { get; } // node-entry + chosen-choice flags, in order
    public bool Advance();      // false when AtChoices || Finished; on the last line of a
                                // linear node: enter NextNodeId (accumulate) or set Finished
    public bool Choose(int index);                    // only when AtChoices; range-checked;
                                                      // accumulates choice flag, enters target node
}
```

Dialogue is atomic: complete or replay — an interrupted session leaves no trace in the model.

### 3.4 `src/Core/DialogueSelector.cs`

```csharp
public static string? ForNpc(string roleId, GameData data, GameTime now);   // total; null = present-but-silent (no Talk prompt)
```

| Role | meeting_done | crew_done (only) | otherwise |
|---|---|---|---|
| `foreman` | `foreman_after` | `foreman_wait` | null (the beat owns the conversation) |
| `crew_worker_a`/`b` | `crew_worker_default` | `crew_worker_default` | null |
| `mayor` | `mayor_after` | null | null |

Beat dialogues (`intro_crew_arrival`, `intro_town_meeting`) are started only by
StoryDirector via `StartDialogue`.

### 3.5 Runtime — WorldSim owns the session (the UI subscribes to the bus)

```csharp
public DialogueSession? ActiveDialogue { get; private set; }
public event Action<DialogueSession>? DialogueStarted;
public event Action<DialogueSession>? DialogueAdvanced;    // after any state change (advance or choice)
public event Action<string>? DialogueFinished;             // def id; AFTER flags applied + phase restored
public bool StartDialogue(string dialogueId);
public bool StartNpcDialogue(string roleId);               // DialogueSelector -> StartDialogue
public void AdvanceDialogue();                             // safe no-op when null/AtChoices
public void ChooseDialogueOption(int index);
```

- `StartDialogue` gates on a new **derived query**: `GameState.CanStartDialogue =>
  Current is Phase.Playing or Phase.Cutscene` (the predicate lives on GameState;
  consumers still never compare Phase). Records `_dialogueFromPlaying =
  GameState.Instance.PlayerHasControl`; if true → `TransitionTo(Phase.Dialogue)`; if
  started inside a beat, phase stays Cutscene and the beat owns the exit.
- On finish: apply `session.FlagsRaised` via `SetStoryFlag` (one bus, one writer — this
  repaints, resyncs NPCs, and fires `StoryFlagSet` per new flag), null the session,
  restore Playing iff `_dialogueFromPlaying`, then fire `DialogueFinished(id)`.
  End-of-session flag application means the NPC you're facing never despawns mid-sentence
  (the clock is frozen in Dialogue/Cutscene, so no tick can move them either).
- `AfterLoad`: null `ActiveDialogue` without applying flags.

### 3.6 UI — `src/UI/DialogueUi.cs : Control`

In `Main.tscn`'s UI layer **between StaminaBar and PauseMenu** (PauseMenu and ScreenFade
draw above; the fade covers the box during beat fades). Code-built in `_Ready` per house
style — `SetAnchorsAndOffsetsPreset` only:

- full-rect root, `MouseFilter.Ignore`, hidden by default;
- `PanelContainer` preset BottomWide, ~84 px tall, side offsets 8/−8/−8;
- speaker `Label` (`NpcDefs.TryGet(role)?.DisplayRole ?? ""`, hidden for narration),
  autowrap body `Label`, "▼" advance hint;
- choices `VBoxContainer` of focusable `Button`s (first grabs focus; VBox neighbors give
  ui_up/ui_down; `Pressed` is a Godot signal — no manual `-=`).

Subscribes `DialogueStarted/DialogueAdvanced/DialogueFinished` + `SaveService.AfterLoad`
(force-hide); all `-=` in `_ExitTree`. Input in `_UnhandledInput`: gated on
`WorldSim.Instance.ActiveDialogue is { Finished: false }`; **ignores the frame the box
opened** (one `ulong _openedFrame` guard — insurance against input-order variance; the
opening E press must not consume line 1); at choices defers to button focus; otherwise
`interact`/`use_tool` → `AdvanceDialogue()` + `SetInputAsHandled()`.

`src/UI/InteractionPrompt.cs`: gains a `GameState.StateChanged` subscription (`-=` in
`_ExitTree`) to hide the prompt while `!PlayerHasControl`.

---

## §4 NPCs

### 4.1 Model — `src/Core/NpcDef.cs`

```csharp
public readonly record struct NpcPlacement(string MapId, int TileX, int TileY, int Facing);
public sealed record ScheduleEntry(
    string? RequiresFlag, string? ForbidsFlag,
    int StartMinuteOfDay, int EndMinuteOfDay,      // inclusive / exclusive; 0..1200, no wrap
    NpcPlacement Placement);
public sealed record NpcDef(string Id, string DisplayRole, string BodyColor,
    IReadOnlyList<ScheduleEntry> Schedule);        // FIRST match wins
```

### 4.2 `src/Core/NpcDefs.cs` — role ids only (names FORBIDDEN)

`mayor` ("Mayor"), `foreman` ("Foreman"), `crew_worker_a` / `crew_worker_b`
("Repair Worker"). Display strings are role labels, `[KEVIN]`. Tunic colors: distinct,
implementer's choice `[KEVIN]`.

### 4.3 `src/Core/NpcSchedules.cs`

```csharp
public static NpcPlacement? Resolve(NpcDef def, GameData data, GameTime now);
```

First entry whose flags pass and whose window contains `now.MinuteOfDay`; null = absent.
Pure, deterministic; NPCs teleport between slots (static staging — no pathing in P3).
**Frozen staging table** (schedule entries in this order per role — first match wins; the
map-authoring sections §2.8/§6 must keep every tile below walkable and obstacle-free;
every `MapId` must be in `MapIds.All` — validated by test). All staging `[KEVIN]`. The
farm crew staging is flag-bounded, not clock-bounded ([0, 1200)) — the beat can never be
stranded castless; the mayor's podium row means the meeting restages **every** pending
evening (missed-meeting recovery is free).

| Role | Requires | Forbids | Window | Placement (map, tile, facing) |
|---|---|---|---|---|
| foreman | `intro.road_cleared` | `intro.crew_arrival_done` | [0, 1200) | farm (33,15) f1 — road mouth |
| crew_worker_a | `intro.road_cleared` | `intro.crew_arrival_done` | [0, 1200) | farm (34,14) f1 |
| crew_worker_b | `intro.road_cleared` | `intro.crew_arrival_done` | [0, 1200) | farm (32,16) f3 |
| mayor | `intro.crew_arrival_done` | `intro.meeting_done` | [720, 1200) | town_hall (20,6) f0 — podium |
| foreman | `intro.crew_arrival_done` | `intro.meeting_done` | [720, 1200) | town_hall (18,12) f3 — seats |
| crew_worker_a | `intro.crew_arrival_done` | `intro.meeting_done` | [720, 1200) | town_hall (20,12) f3 |
| crew_worker_b | `intro.crew_arrival_done` | `intro.meeting_done` | [720, 1200) | town_hall (22,12) f3 |
| mayor | `intro.road_cleared` | — | [120, 660) | town (24,19) f0 — square |
| foreman | `intro.crew_arrival_done` | — | [120, 600) | town (30,13) f0 — roadside |
| crew_worker_a | `intro.crew_arrival_done` | — | [120, 600) | town (31,16) f3 |
| crew_worker_b | `intro.crew_arrival_done` | — | [120, 600) | town (33,13) f0 |

### 4.4 Ticker — WorldSim (entities never touch time events; MinuteTicked stays HUD-only)

```csharp
public void SyncNpcsNow();   // per NpcDefs.All: NpcSchedules.Resolve(...);
                             // per registered, non-dying map: map.SyncNpcs(entries for that map)
```

Called from: `TenMinuteTicked` (new subscription, `-=` in `_ExitTree`), `OnDayStarted`
step 4 (`AdvanceToDayStart` fires no ten-minute ticks — dawn would otherwise be stale
until 6:10), `SetStoryFlag` (on new sets), `CompleteTravel`, and `SaveService.AfterLoad`
(new subscription; autoload order GameState→Clock→SaveService→WorldSim makes it safe).

### 4.5 Views — MapRoot base additions (non-virtual)

```csharp
private readonly Dictionary<string, NpcView> _npcViews = new();
public void SyncNpcs(IReadOnlyList<(NpcDef Def, NpcPlacement Placement)> forThisMap);
    // diff: spawn missing at tile center (x*16+8, y*16+8); move/reface changed;
    // QueueFree departed AND remove from the dict immediately (the node lingers to end of frame)
public NpcView? GetNpcView(string roleId);   // test seam
```

`src/World/NpcView.cs` — `Area2D, IInteractable`, pure view, subscribes to nothing:

```csharp
public string RoleId { get; init; } = "";
public string PromptText => "Talk";
public bool CanInteract(Node2D i) => GameState.Instance.PlayerHasControl && !IsQueuedForDeletion()
    && DialogueSelector.ForNpc(RoleId, SaveService.Instance.Current, Clock.Instance.Now) != null;
public void Interact(Node2D i) => WorldSim.Instance.StartNpcDialogue(RoleId);
```

Area: layer 2, mask 0, monitorable, `CollisionShape2D` 16×22. Child `StaticBody2D`
blocker (layer 1, 12×8 at the feet) so NPCs block movement like the Bed. The
`IsQueuedForDeletion` guard closes the one-frame freed-but-overlapped probe window.

Sprite via new `src/World/PlaceholderSprites.cs`:
`public static ImageTexture Character(int facing, Color tunic)` — extracted from
`PlayerController.CreateFacingTexture` with the tunic color parameterized;
**PlayerController is refactored to call it too**.

### 4.6 Persistence: nothing

Presence/position/facing are a pure function of `(StoryFlags, GameTime)`; reload
reconstructs bit-identically. Relationships/connections are Phase 3b saved data,
deliberately absent from v3.

---

## §5 Scripted beats — StoryDirector

```csharp
private bool _beatRunning;
private TaskCompletionSource? _dialogueDone;

private void ScheduleCheck() => CallDeferred(nameof(CheckTriggers));
private void CheckTriggers()
{
    if (_beatRunning || WorldSim.Instance.ActiveDialogue != null) return;
    if (!GameState.Instance.PlayerHasControl) return;
    string mapId = SaveService.Instance.Current.Player.MapId;
    if (!WorldSim.Instance.IsMapActive(mapId)) return;
    StoryBeatId? beat = IntroRules.PendingBeat(SaveService.Instance.Current, Clock.Instance.Now, mapId);
    if (beat == StoryBeatId.CrewArrival)  _ = RunBeat("intro_crew_arrival");
    if (beat == StoryBeatId.TownMeeting)  _ = RunBeat("intro_town_meeting");
}

private async Task RunBeat(string dialogueId)
{
    _beatRunning = true;
    GameState.Instance.TransitionTo(GameState.Phase.Cutscene);   // clock + player frozen; tree NOT paused
    try
    {
        WorldSim.Instance.SyncNpcsNow();                          // staging certain (crew at road / mayor at podium)
        await ToSignal(GetTree().CreateTimer(0.4), Timer.SignalName.Timeout);   // one beat of static staging
        WorldSim.Instance.StartDialogue(dialogueId);              // legal from Cutscene via CanStartDialogue
        await _dialogueDone!.Task;                                // completed by the DialogueFinished handler
        // completion flag applied by the session's terminal SetsFlag through SetStoryFlag —
        // already repainted + resynced (crew departs).
    }
    catch (Exception e) { GD.PushError($"Beat '{dialogueId}' failed: {e}"); }
    finally { _beatRunning = false; GameState.Instance.TransitionTo(GameState.Phase.Playing); }
}
```

The `finally` triggers another deferred check — idempotent (completion flag blocks
`PendingBeat`). No cutscene VM, no scripted movement, no camera pans — this two-phase
pattern (Cutscene framing, Dialogue conversation) is what Phase 5 extends.
`AfterLoad` handler: cancel `_dialogueDone`, clear `_beatRunning`, set no flags.

**Interruption matrix — spec invariant: *no save can exist with a beat half-done*:**

- Sleep during a beat: impossible (`Bed.CanInteract` requires Playing; player frozen).
- Pause/manual save: impossible (PauseMenu inert outside Playing/Paused).
- Travel during a beat: impossible (`RequestTravel` and `Door.CanInteract` require `PlayerHasControl`).
- DayStarted during a beat: impossible (Cutscene ⇒ `!ClockRuns`; only the sleep flow advances days).
- Quit mid-beat: last save is the morning autosave (arrival flags in, completion absent)
  → reload re-derives `PendingBeat` and replays the beat from the top.
- Load mid-beat (test harness): director aborts cleanly via `AfterLoad`; DialogueUi
  force-hides; WorldSim nulls the session.
- Double-fire: `_beatRunning` + `ActiveDialogue` guard + only-if-absent `TrySetFlag` +
  completion-flag-blocked `PendingBeat` — four independent layers.

---

## §6 Road state (TestMap)

- Obstacles atlas gains tile index 6 **`Debris`** (`#5a4a2e`, deterministic log/branch
  speckle), walkable=false, full-square collision — config identical to Water/Stone.
- Road surface: the water border ring opens at rows y=14–15, x=38–39 (dirt ground, no
  obstacle); a dirt strip connects the field east to the edge.
- `private static readonly Vector2I[] RoadBlockCells = { (36,14),(36,15),(37,14),(37,15) };`
- Road + blockade cells (x36..39 × y14..15) join `_reservedTiles` (never tillable).
- A `MapExit` over (38–39, 14–15) → `town`/`from_farm`, with
  `IsEnabled = () => SaveService.Instance.Current.HasFlag(StoryKeys.RoadCleared)` — belt
  (tile collision) and suspenders (disabled exit): even a clipped-through player cannot
  transition early. Spawn `road` at tile (35,15) center.
- A `Sign` at the blockade: `[KEVIN]` "The storm brought half the hillside down. No
  getting through today." (canon restatement only).

Toggle, inside `ApplyState`, after the existing FarmSoil/Crops repaint (same precedent as
wet/dry soil reading `Clock.Now` at refresh time — a view-side model read):

```csharp
bool cleared = SaveService.Instance.Current.HasFlag(StoryKeys.RoadCleared);
foreach (var c in RoadBlockCells)
    if (cleared) _obstacles!.EraseCell(c);
    else         _obstacles!.SetCell(c, 0, new Vector2I(Debris, 0));
_roadExit.SetDeferred(Area2D.PropertyName.Monitoring, cleared);
```

Every flag-changing path already repaints (dawn ordering §1.4; `SetStoryFlag`). The road
never touches `MapState` — Phase-2 purity tests stay bit-identical.

---

## §7 Save v3

- **Schema delta: exactly one field** — `GameData.StoryFlags` (§1.1). No
  `SaveJsonContext` edit needed; `Save_NoGodotTypesInDtos` covers it automatically.
- `src/Core/MigrationV2ToV3.cs` — frozen literal, only-if-absent, zero live-code calls:

```csharp
public sealed class MigrationV2ToV3 : ISaveMigration
{
    public int FromVersion => 2;
    public void Apply(JsonNode root)
    {
        if (root["StoryFlags"] is null)
            root["StoryFlags"] = new JsonObject();
    }
}
```

- `SaveMigrations`: `CurrentVersion = 3`; `Chain = [new MigrationV1ToV2(), new MigrationV2ToV3()]`.
- **Recorded consequence**: migrated v1/v2 saves get empty flags and replay the intro
  road-blocked, even with planted crops (`intro.first_planting` absent until the next
  planting). A frozen literal cannot compute "has planted" without calling live code.
  Accepted for dev-only saves `[KEVIN]`; revisit before any public build.
- `GameData.NewGame()`: no change — zero flags IS the "morning after the storm" state.
- `SaveService.DeserializeFrom` pre-swap repairs (with the existing validation block):

```csharp
data.StoryFlags ??= new();
data.StoryFlags.Remove("");
foreach (string k in data.StoryFlags.Keys.ToList())
    if (data.StoryFlags[k] < 0) data.StoryFlags[k] = 0;
```

Unknown keys preserved verbatim (round-trip tested). A JSON `null` flag value fails typed
deserialization → existing Corrupt + quarantine path.

- Fixture `src/Tests/fixtures/v3_minimal.json` — **byte-frozen at creation** (the v4
  migration's input), freezing a meeting-pending mid-story state with an unknown flag:

```json
{"SaveVersion":3,"TotalMinutes":3600,"Player":{"MapId":"town","X":56,"Y":232,"Facing":2,"HasPosition":true,"Money":760,"Stamina":80,"MaxStamina":100,"Inventory":{"Slots":[{"ItemId":"hoe","Count":1},null,null,null,null,null,null,null,null,null],"SelectedSlot":0}},"Maps":{"test_farm":{"Tiles":[{"X":5,"Y":6,"Kind":"tilled","CropId":"turnip","GrowthDay":3,"LastWateredDay":2}],"Objects":[]}},"ShippingBin":[],"StoryFlags":{"intro.first_planting":1,"intro.road_cleared":2,"intro.crew_arrival_done":2,"future.mystery_flag":9}}
```

- **Drift guards**: existing `Save_MigratedKitMatchesNewGame` untouched and green; new
  `Save_MigratedStoryMatchesNewGame` — migrated-v2 `StoryFlags` equals
  `GameData.NewGame().StoryFlags` (both empty). Either guard failing means a conscious
  decision — **never** an edit to a frozen migration.

---

## §8 Tests

`TestRunner.MinimumExpectedTests`: set to the exact shipped [SimTest] count, which must be
**≥ 55** (37 existing + the plan below). Harness rules unchanged (`NewGame()` +
`TransitionTo(Playing)` in `finally`; Main-booting tests use the existing cleanup that
also deletes the autosave). New files: `StoryTests.cs`, `DialogueTests.cs`, `NpcTests.cs`,
`TravelTests.cs`; additions to `SaveTests.cs` / `IntegrationTests.cs`.

**Required modifications to existing tests (conscious, commented edits):**

- `Integration_FullFarmLoop`: immediately after boot, stamp `intro.crew_arrival_done` and
  `intro.meeting_done` via `WorldSim.SetStoryFlag` with the comment "intro beats disabled:
  this test exercises the farm loop, not the story". **Without this the crew beat fires
  the morning after its first planting and the WaitUntil(Playing) hangs.** The road still
  clears mid-test — assert that this is harmless. Standing rule going forward: any
  Main-booting test that plants and sleeps must pre-stamp the intro completion flags or
  drive the dialogue.
- `Events_MapSwapStress`: pre-stamp crew flags once; each swap cycle calls
  `WorldSim.SyncNpcsNow()` between instance and free (leaked NPC views or stale map
  references must crash a later cycle). No Main is booted, so no beats fire.

**New tests (pure Core):**
1. `Story_FlagStampAndIdempotence` — TrySetFlag once; stamp preserved; FlagDay −1 when absent.
2. `Story_RoadClearRules` — no planting ⇒ empty every dawn (days 1..10); plant day 0 ⇒
   cleared exactly at dawn 1; plant day 4 ⇒ dawn 5; already-cleared ⇒ empty; post-midnight
   planting clears the next dawn.
3. `Story_RulesTotalOnHostileFlags` — all 2⁴ intro-flag combos + unknown keys + stamps
   {0, clamped, 999999}: `FlagsToSetOnDayStarted`/`PendingBeat` never throw.
4. `Story_PendingBeatMatrix` — beats × maps × MinuteOfDay 719/720/1199; nothing pends after completion.
5. `Story_MeetingRecursNightly` — pending across three simulated evenings (no day term).
6. `Dialogue_DefsValidate` — StartNodeId + every NextNodeId/choice target resolve; no
   node has both NextNodeId and Choices; no empty Lines; all nodes reachable; every
   SetsFlag is a StoryKeys constant; every non-empty SpeakerRole resolves in NpcDefs;
   **every terminal path of each beat dialogue sets its completion flag** (DFS).
7. `Dialogue_SessionWalkAndBranch` — linear walk; both forks; convergence; exact
   FlagsRaised; Advance false at choices; Choose range-checked.
8. `Npc_ScheduleResolveDeterministic` — same (flags, time) ⇒ bit-equal placement twice;
   start-inclusive/end-exclusive boundaries; first-match priority; null when absent;
   every placement MapId ∈ MapIds.All.
9. `Npc_IntroStaging` — crew on farm iff `road_cleared && !crew_done`; mayor at podium
   iff meeting pending in-window; nobody on the farm after `crew_done`.

**Save:**
10. `Save_MigrationV2ToV3` — frozen v2 fixture ⇒ v3, StoryFlags present+empty, full v2
    payload intact; re-serialize ⇒ idempotent.
11. `Save_MigrationChainV1ToV3` — v1 fixture ⇒ v3 + starter kit + empty flags + v1 payload.
12. `Save_StoryFlagsRoundTrip` — unknown key survives byte-exactly; negative stamp clamps
    to 0; `""` key dropped; extend `Save_RoundTrip` with a populated flag.
13. `Save_FixtureV3Loads` — frozen v3 fixture ⇒ meeting pending per `PendingBeat`.
14. `Save_MigratedStoryMatchesNewGame` — drift guard.

**Scene/system:**
15. `Map_RegistryCreatesAll` — every id instances under Host; MapId set; every documented
    spawn non-fallback; camera limits ≥ 640×360.
16. `Map_ExitDisabledWhileBlocked` — fresh game, player teleported onto the road exit ⇒
    no transition; debris cells present.
17. `Story_RoadRepaintOnFlag` — blockade cells present ⇒ `SetStoryFlag(RoadCleared)` ⇒
    `GetCellSourceId == -1`; `StoryFlagSet` fired exactly once; second call false, no event.
18. `Story_PlantingSetsFlagOnce` — till+plant via `UseSelectedItem` ⇒ stamped today;
    second plant ⇒ unchanged.
19. `Npc_ViewSpawnDespawn` — crew-pending flags + `SyncNpcsNow` ⇒ NpcView at scheduled
    tile center with "Talk" prompt; `crew_done` ⇒ gone after one frame.
20. `Travel_SwapsMapAndModel` — boot Main; stamp flags; `RequestTravel(Town, "from_farm")`;
    WaitUntil MapId/spawn/phase; back again; `IsMapActive` correct both ways; **no
    spurious second transition on arrival** (spawn-clearance rule); no autosave written.
21. `Travel_RefusedWithoutControl` — during dialogue `RequestTravel` returns false, no
    event; after finish ⇒ works.
22. `Dialogue_WorldSimFlow` — start ⇒ `ClockRuns` false, UI visible, **first line still
    current after start** (no opening-press double-fire); drive to completion ⇒ flags via
    bus, phase restored, `ActiveDialogue` null; `AdvanceDialogue` with no session is a
    safe no-op; `AfterLoad` force-hides.
23. `Story_QuitMidBeatRetriggers` — enter crew beat, don't finish; the last autosave
    content lacks the completion flag; `DeserializeFrom` ⇒ director aborted cleanly,
    `PendingBeat == CrewArrival`.
24. `Integration_FullIntro` — headless capstone: boot fresh ⇒ blockade present; **sleep
    without planting ⇒ morning 2 still blocked**; till/plant/water via WorldSim; sleep ⇒
    `road_cleared` stamped with the new day's index, blockade gone, **the autosave file
    already contains the stamp**; WaitUntil crew beat (`ActiveDialogue != null`); assert
    no clock advance while dialogue active; pump `AdvanceDialogue`/`ChooseDialogueOption`
    (choice 0) ⇒ `crew_arrival_done`, Playing; travel farm→town→hall **before 18:00 ⇒ no
    beat**; `AdvanceMinutes` past 720 inside ⇒ meeting fires; complete ⇒ `meeting_done`;
    travel home, sleep ⇒ autosave; `Load()` fresh ⇒ all stamps exact, `PendingBeat ==
    null`, no beat re-fires after several frames.
25. `Integration_MeetingMissedRecovers` — from crew-done, sleep through a night; next
    evening in the hall the beat fires.

**Screenshot verifiability:** `Main.HandleCmdlineArgs` gains dev-only `--start-map <id>`
(override `Current.Player.MapId`, clear `HasPosition`, before `LoadMap`) so
`--screenshot` can capture town and town-hall placeholders.

---

## §9 [KEVIN] review ledger (all placeholder, awaiting Kevin)

1. Meeting start hour — 6:00 PM is a mechanical placeholder.
2. The meeting recurs nightly until attended — want a line of lore cover, or silent
   recurrence (current design)?
3. All dialogue copy (crew arrival incl. both fork branches; town-hall Q&A restating the
   three canon facts; wait/after/ambient lines; any narration).
4. Road-debris sign text.
5. Display role labels ("Mayor", "Foreman", "Repair Worker") until names are decided.
6. Repair-crew composition (foreman + two workers) is invented staging.
7. Post-intro NPC whereabouts (filler schedule rows) are invented staging.
8. Should the player's chosen reaction to the crew be recorded as a flag for later
   callbacks? (Mechanism exists: `DialogueChoice.SetsFlag`.)
9. Migrated v1/v2 dev saves replay the intro with the road re-blocked (frozen-migration
   constraint) — confirm acceptance.
10. NPC tunic colors.

## §9b As-built addenda (integration + adversarial review, 2026-08-25)

Deltas from the spec text above, all shipped and test-covered (63 tests total):

- **Closing-press guard** (review, high): the press that closes a dialogue is handled in
  `_UnhandledInput` but still reads as just-pressed in the same frame's
  `_PhysicsProcess` poll, and `FinishDialogue` restores Playing synchronously — the
  closing E re-opened ambient dialogues in a loop (and a closing click swung the tool).
  `PlayerController` now swallows interact/use_tool on the first physics frame control
  returns (`_hadControlLastPhysicsFrame`).
- **Probe hardening** (review): `InteractionProbe` focus now consults
  `CanInteract` (silent NPCs — `DialogueSelector` null — show no Talk prompt, honoring
  §3.4), and `TryInteract` guards `IsInstanceValid` against an NPC freed between the
  parent's poll and the probe's re-poll.
- **Known-flag stamp clamp** (review, §7 repair extended): future-dated stamps on the
  four known intro flags clamp to the save's own day (a future `first_planting` wedged
  the road forever — only-if-absent flags cannot be restamped). Unknown flags keep
  stamps verbatim.
- **StoryDirector load epoch** (review, §5 hardened): `_loadEpoch` increments per
  AfterLoad; a beat whose staging await spans a load bails before `StartDialogue`. The
  TCS is also created before the staging await so cancellation lands during it.
- **WorldSim.OnAfterLoad phase restore** (review): a load during a Playing-started
  dialogue now restores Playing when discarding the session (was a Dialogue-phase strand).
- **Blockade sign** toggles off with the debris in `ApplyState` (its copy is only true
  while blocked).
- **Boot goes through the bus**: `Main.LoadMap` calls `CompleteTravel(map.MapId)` —
  stages NPCs at boot (AfterLoad fires before any map registers) and rewrites
  `Player.MapId` coherently after an unknown-id fallback. Recorded consequence: the
  unknown map-id STRING in Player.MapId is overwritten on the next autosave (its
  MapState is still preserved) — accepted, an incoherent Player.MapId dead-ends beats
  and tools.
- **`--screenshot-frames <n>`** dev flag added beside `--screenshot`/`--start-map`.
- `MinimumExpectedTests` = 63 (62 planned + `Save_FutureIntroStampsClamped`).
- Pre-existing Phase 2 tests `Save_FixtureV2Loads` / `Save_MigrationV1ToV2` now assert
  `SaveMigrations.CurrentVersion` (their job is payload survival, not pinning the
  chain's endpoint).

## §10 Deferred ledger

- `test_farm` → `farm` id rename: ships with the first editor-authored farm map, as its
  own migration (Maps key + Player.MapId rewrite).
- Arm-after-clear exit re-trigger poll (adopt only if the spawn-clearance rule proves
  insufficient in a test).
- Per-line (immediate) dialogue flag application — `DialogueNode.SetsFlag`'s application
  point can move without schema change if Phase 5 needs mid-cutscene world changes.
- Manual beat re-trigger via NPC talk (two-line `DialogueSelector` addition if wanted).
- Autosave-on-travel (must re-prove the beat-atomicity invariant if added).
- Relationships/connections saved data (Phase 3b).
- NPC pathing/walk animation; cutscene VM with movement steps; camera pans.
