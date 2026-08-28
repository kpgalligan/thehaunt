# src/UI — the HUD and menu layer

Eleven code-built Controls in Main.tscn's UI layer, each building its own controls in
`_Ready`: Hud, InteractionPrompt, HotbarUi, StaminaBar, HelpPanel, DialogueUi, ChestUi,
ShopUi, OvernightReportUi, PauseMenu, ScreenFade. Every panel is a pure view over
system events (WorldSim sessions, GameState.StateChanged, SaveService.AfterLoad) —
no UI owns durable state or mutates the model directly.

## Rules (violations are bugs)

- Chest/shop UIs run in the Menu phase, owned by WorldSim's Open*/Close* sessions
  (OpenStorageId/OpenShopId) — UIs never call TransitionTo themselves; Menu freezes
  clock and player but never tree pause. Modal UIs replicate DialogueUi's
  `_openedFrame` guard (today: ChestUi and ShopUi): the press that OPENED the panel may
  still be dispatching that frame and must not also transfer/buy/advance.
- The overnight report is awaited INSIDE Main.RunSleepFlow while the phase is still
  Sleeping (report before Playing, then a 0.3 s mash-grace); OvernightCompleted itself
  fires mid-advance while the screen is black — latch, never display, in the handler.
- Code-built Controls: use `SetAnchorsAndOffsetsPreset(...)` to lay out, never
  `SetAnchorsPreset(...)` — the latter keeps the control's current rect (zero for a
  fresh Control) by compensating offsets, which silently produces invisible
  zero-size UI.
- UI is built in code at viewport pixel sizes, so it does not rescale with the
  viewport: `gui/theme/default_theme_scale` carries the built-in theme, and explicit
  font sizes and widget constants are tuned by hand. Re-check every UI screenshot if
  the viewport changes.
- Hud is the one legitimate `MinuteTicked` subscriber — MinuteTicked is display-only;
  everything else uses `TenMinuteTicked` (src/CLAUDE.md).
- Unknown item ids render as a '?' placeholder — never dropped, never thrown on
  (HotbarUi/ChestUi).

## Layer facts (from the code)

- Stack order: DialogueUi sits between StaminaBar and PauseMenu; ChestUi/ShopUi sit
  above DialogueUi and below PauseMenu/ScreenFade — pause overlay and fade draw on top.
- HelpPanel is pure non-modal: no phase change, root ignores mouse ALWAYS, nothing
  takes focus; it force-hides whenever the new phase lacks control.
- ScreenFade animates a manual frame loop, never a Tween — a node-bound tween killed
  on free never fires Finished, which would hang the awaiting sleep flow.
- PauseMenu visibility is driven ONLY by GameState.StateChanged, so every entry
  into/out of Paused (from any caller) shows/hides it consistently.
