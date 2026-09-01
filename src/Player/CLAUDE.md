# src/Player — the player body

PlayerController (CharacterBody2D): movement, tool targeting, hotbar input, and the
scooter interact semantics; builds its own CharacterSprite (Jane's sheet is the
default), feet collider, Camera2D, and the InteractionProbe (Area2D) that owns
interaction focus and drives the prompt.

## Rules (violations are bugs)

- PlayerController is the ONLY IPersistentSystem in the game: save state lives in the
  central GameData model and scenes are views rebuilt from it. Only the player node
  registers with SaveService.
- InteractionProbe focus consults CanInteract — a focused-but-refusing candidate
  renders a lying prompt (silent NPCs are the live case) — and TryInteract guards
  freed nodes: Focused can point at a node freed since the last poll.
- Gate on GameState.PlayerHasControl, never by comparing the Phase enum. The first
  physics frame control RETURNS swallows action presses: the E that closed a dialogue
  still reads just-pressed and would re-open the conversation (a closing click would
  swing the tool).
- TargetTile() = feet tile + facing direction, computed directly — never from the
  probe position (feet + dir*14 rounds back into the player's own tile when
  feet%16 < 2).
- Interact key, three meanings (scooter handoff): a focused interactable always wins;
  mounted with nothing focused, E parks the scooter on the tile under the feet
  (WorldSim.DismountScooter); on foot with nothing focused it does nothing. Mounted
  state is read from the model every physics frame, never event-plumbed, so a load,
  NewGame, or overnight reset stays correct; riding multiplies MoveSpeed (80 px/s)
  by ScooterRules.SpeedMultiplier.
- All gameplay mutations go through WorldSim (SelectSlot, UseSelectedItem, scooter
  calls) — the controller never writes the model outside Write/ReadState.
- The work loop (tools handoff): a tool with an authored work sheet
  (CharacterSprites.WorkSheet != null) swings through Core's WorkAnimation on
  use_tool — the player is PLANTED for the swing (no walking, interact, or hotbar),
  the target tile is locked at the windup, and WorldSim.UseSelectedItem fires on
  ENTRY to the impact frame, never at press time. Everything else — seeds, the
  scythe, any use while riding — keeps the instant path. A press COMMITS one full
  cycle — a tap is one completed action (Kevin's amendment; the handoff's
  release-before-impact cancel is dropped); holding repeats, a held direction ends
  the loop at the cycle boundary, and losing PlayerHasControl mid-swing is the one
  thing that cancels the pose outright. All test-pinned in WorkAnimation.

## Geometry (from the code)

- Feet offset +6 px; feet collider 12x8 on layer/mask 1. Probe: reach 14 px, radius 8,
  mask 2 (interactable areas); polls overlaps each physics frame instead of enter/exit
  signals. Tool use cooldown 0.25 s.
- hotbar_1..hotbar_10 are polled in _PhysicsProcess; hotbar_next/prev are handled in
  _UnhandledInput because wheel actions land press+release within one frame.
