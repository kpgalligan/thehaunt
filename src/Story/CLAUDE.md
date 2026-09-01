# src/Story — scripted-beat orchestration

One file: `StoryDirector` — a plain Node child of Main, NOT an autoload; it dies with
Main, so headless tests that never boot Main never evaluate beats. It runs the scripted
intro beats (`intro_crew_arrival`, `intro_town_meeting`,
`intro_town_meeting_overslept`), derived per check from
`IntroRules.PendingBeat(save, now, mapId)` in Core. It never writes the model: flags
flow through `WorldSim.SetStoryFlag` and the dialogue's terminal SetsFlag.

## Rules

- Story beats start ONLY via StoryDirector's CallDeferred check — never synchronously
  from StateChanged (nested TransitionTo) or TenMinuteTicked (Accumulate keeps ticking
  after a mid-loop phase change). Every trigger source (StateChanged, TenMinuteTicked,
  StoryFlagSet, AfterLoad) funnels through the same deferred `CheckTriggers`.
- CheckTriggers refuses when a beat is running, a dialogue is active, the player lacks
  control, or the player's map is not the active registered map.
- Beat shape: TransitionTo(Cutscene) — clock + player frozen, tree NOT paused —
  `SyncNpcsNow` for certain staging, a 0.4 s static-staging timer, then `StartDialogue`
  (legal from Cutscene via `CanStartDialogue`); the completion flag lands through the
  session's terminal SetsFlag, and the `finally` always restores Playing.
- Load mid-beat aborts cleanly via a load epoch: AfterLoad cancels the awaited dialogue
  task, no flags are set, and the loaded save re-derives any pending beat on the next
  deferred check (replay-from-autosave — a documented path, not a failure).
- The overslept wake: going to bed with the summons heard but the meeting pending
  (`IntroRules.WakesAtTownHall`) relocates the wake to the town hall — Main's sleep
  flow stamps `intro.overslept` and swaps the map behind the sleep fade, BEFORE the
  morning autosave, and the overslept beat (narration line, then the shared meeting
  nodes) fires off the director's normal deferred check. The flag also holds the
  mayor at the podium around the clock (NpcSchedules), so the dawn meeting is never
  castless. This supersedes phase3-spec's "missed-meeting recovery is free" line —
  the recovery is now scripted, not passive.
- Any Main-booting test that plants and sleeps must pre-stamp the intro completion
  flags (crew_arrival_done, meeting_done) or drive the dialogue — otherwise the crew
  beat fires on the morning after and WaitUntil(Playing) hangs; with only
  crew_arrival_done stamped, the sleep itself relocates the wake to the hall and the
  overslept beat fires instead.
