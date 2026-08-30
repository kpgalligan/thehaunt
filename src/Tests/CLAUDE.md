# src/Tests — the headless [SimTest] suite

A custom harness, not xUnit: `TestRunner` (root of `scenes/tests/TestRunner.tscn`)
discovers every static method tagged `[SimTest]` in the assembly, runs them sequentially
in name-sorted order, prints one PASS/FAIL line per test plus a RESULT summary, and
quits with exit code 0/1. Each test takes a `TestContext` (Assert/AssertEqual,
WaitFrames, frame-polling WaitUntil with a 5 s default timeout; `Host` is the runner
node for adding test scene instances). `fixtures/` holds frozen v1–v6 minimal save
JSONs for the migration tests.

- Run: `godot-mono --headless res://scenes/tests/TestRunner.tscn` (exit code 0/1). Run
  `godot-mono --headless --import` first after adding/changing assets or scenes.
- TestRunner pins `MinimumExpectedTests` against silent discovery breakage — re-pin to
  the exact count whenever tests ship.
- Save IO is isolated: the runner sets `SaveService.DefaultSlot = "test_autosave"` and
  deletes test_* saves before and after the run.

## Rules (violations are bugs)

- Any Main-booting test that plants and sleeps must pre-stamp the intro completion flags
  (crew_arrival_done, meeting_done) or drive the dialogue — otherwise the crew beat
  fires on the morning after and WaitUntil(Playing) hangs. (Stamp both back-to-back so
  no beat slips in between the deferred trigger checks — see TravelTests for the pattern.)
- Any test owning the garage deed whose clock can cross open hours must pin the
  arrival dice: set `data.Seed` to a scanned quiet value (IntegrationTests.QuietSeed)
  or inject `GarageJobs` records directly — SaveService.NewGame rolls a RANDOM seed,
  and an unpinned 6%/hour roll is a flake that lands an extra car/toast/log row.
  Model-side tests use `GameData.NewGame()` (seed 0) or scan for an arriving seed.
- Presses aimed at the controller's PHYSICS poll (E via IsActionJustPressed) can
  phase-lock against PressKey's fixed process-frame cycle — landing every press or
  NONE, deterministically by starting alignment. Use IntegrationTests.PressKeyRobust
  (spans a physics frame and a process frame per edge) for E; process-side
  _UnhandledInput keys (J/K/Tab/Esc) are fine with plain PressKey.
- Drift guards exist to force a conscious decision, never a mechanical fix:
  Save_MigratedKitMatchesNewGame / Save_MigratedStoryMatchesNewGame failing means decide
  deliberately — never edit a frozen migration. MapIds.IsInterior carries a drift guard
  against each map's IsInterior; TileSetReloadTests guards the TileSet builders'
  private-copy (CacheMode.Ignore) + idempotency contract; MapSeedTests guards recipe
  seeds against their C# literals until a map's file and seed part company on purpose.
- SourceRulesTests reads the source tree itself, for standing rules nothing else can
  catch (breaking them compiles and passes every other test): src/Core stays free of
  `using Godot`, and `Engine.IsEditorHint()`/`[Tool]` stay out of the game layers
  (src/EditorTools and addons only). Its file counts are asserted non-zero first on
  purpose — a test that silently found zero files would pass forever.
