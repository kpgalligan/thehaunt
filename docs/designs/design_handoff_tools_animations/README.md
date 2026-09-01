# Handoff: Tools & Work Animations

## Overview

Jane can clear land and water crops, but the actions currently resolve with no visible work. This bundle adds four tools with tiered upgrades and a four-frame work animation for each, baked into Jane's character frames:

| Tool | Action | Sheet |
|---|---|---|
| Hoe | Clearing land / tilling soil | `tool_hoe.png` |
| Watering can | Watering crops | `tool_can.png` |
| Axe | Clearing trees | `tool_axe.png` |
| Pickaxe | Clearing rocks | `tool_pick.png` |

Each tool has three tiers — **basic** (inherited with the farm), **dad-level** (what you'd find in an average suburb garage), and **pro**. Farming is not the primary revenue source in The Haunt, so the ladder stops at three rungs by design; do not add a fourth without a design conversation.

## About the Design Files

The files in this bundle are **design references created in HTML and generated pixel art** — a prototype review page plus the sprite sheets and the generator that produced them. The HTML page (`The Haunt - Tools and Work Animations.dc.html`) is not production code; it exists so a reviewer can watch the animations loop and read the sheet contract.

The PNG sheets, however, **are** shippable assets: import them into the engine as-is. The generator (`gen_tools.js`) is the source of truth for their pixels — never hand-edit the PNGs, edit the generator and re-run it.

The task in the codebase is to wire these sheets into the existing animation, tool-inventory, and tile-interaction systems using the project's established patterns.

## Fidelity

**High-fidelity.** The sprite sheets are final pixel art at the game's authored resolution. Colors, frame counts, cell geometry and row order below are exact and should be treated as a contract. The review HTML page is hifi for review purposes only.

---

## Sheet contract

Every tool sheet is identical in geometry.

- **Cell:** 16 × 32 px (matches the existing cast sheets; `character.png` uses the same cell)
- **Sheet:** 64 × 192 px
- **Columns (4):** animation frames, left to right
- **Rows (6):** tier × facing

### Columns — frame semantics

| Col | Frame | Meaning |
|---|---|---|
| 0 | Windup | Tool raised, weight back |
| 1 | Strike | Tool descending through the arc |
| 2 | Impact | Tool head at ground level, body leaned in 1 px |
| 3 | Recover | Tool lifting back to neutral |

### Rows — tier × facing

| Row | Y offset | Contents |
|---|---|---|
| 0 | 0 | basic, facing down |
| 1 | 32 | basic, facing side (right) |
| 2 | 64 | dad-level, facing down |
| 3 | 96 | dad-level, facing side (right) |
| 4 | 128 | pro, facing down |
| 5 | 160 | pro, facing side (right) |

**Left facing:** mirror the side rows horizontally. No separate art.

**Up facing:** not authored. Reuse the down rows (the tool arcs to Jane's right, so it reads acceptably from behind) or request the rows — the generator can produce them from the existing `headUp`/`torsoDown(back)` primitives.

---

## Animation behavior

- **Loop:** frames 0 → 3, repeat while the work action is held.
- **Suggested timing:** 90 / 90 / 140 / 90 ms. The impact frame holds longer; that hold is what sells the hit.
- **Gameplay hook:** fire the tile mutation (till, water, tree felled, rock broken) on **entry to frame 2 (impact)**, not on action start and not at loop end.
- **Interruption:** if the player releases or moves during frames 0–1, cancelling to idle is fine. Once frame 2 has fired, let the loop finish frame 3 so the tool visibly returns.
- The review page loops all four frames at an even rate for convenience; even timing is *not* the intended in-game feel.

### Two-handed grip

Every frame puts both hands on the tool. The lead hand takes the grip point; the off hand sits two pixels up the shaft, so the stagger reads as a real grip rather than a floating prop. The watering can has one hand on the carry handle and a second braced under the body. If a future tool is added, keep this rule — a one-handed farm pose was rejected in review.

---

## Tier differentiation

Tiers differ by **head material only**. Handles, shaft length, grip points, silhouettes, frame counts and timing are identical across tiers. This is deliberate: an upgrade reads instantly without changing collision, reach, or animation timing, so tier is a pure stat/visual change in code.

| Tier | Head base | Head highlight | Head shade | Reads as |
|---|---|---|---|---|
| basic | `#7a6a5c` | `#a89a88` | `#3a322c` | Pitted, half-rusted iron |
| dad-level | `#b8b5a5` | `#f0f3ee` | `#4e524f` | Clean stamped steel |
| pro | `#575a58` | `#8a8f8c` | `#22262a` | Dark forged steel |

Handle (all tiers): base `#6b4a2f`, highlight `#a5855c`.
Sprite outline: `#2b241d`.
Water (can pour only): `#47788c`, deep `#2e5566`.

All values are drawn from the existing project palette in `gen_cast.js` — no new colors were introduced.

---

## Watering can — one open decision

The pour frame (col 2) includes a short water column at the spout, 4 px tall with a 1 px side spray. Without it the pose reads as *holding* a can rather than *watering*.

The art direction answer was "no effects — the engine handles particles." If the particle system should own the water entirely, delete the `if(k.stream){ … }` block in `gen_tools.js` and re-run; nothing else depends on it. Splash, dirt puffs, wood chips and rock sparks are **not** in the sheets and are expected from the engine.

---

## Integration notes for the codebase

1. **Cell height.** These sheets use the 16 × 32 cell established by the cast sprite update. If any tool/equipment code still assumes the old 16 × 22 cell, it needs the same fix already applied to the cast sheets.
2. **One sheet per tool.** The animation is baked into Jane's frames, so there is no separate tool overlay layer to composite and no draw-order problem to solve. Selecting a tool + tier + facing is a sheet + row selection.
3. **Row lookup.** `row = tierIndex * 2 + (facing === 'side' ? 1 : 0)`, where `tierIndex` is 0 basic, 1 dad, 2 pro.
4. **Tool state needed:** equipped tool (`hoe | can | axe | pick | none`), tier per owned tool, and an action phase (frame index + elapsed ms). Tier is per-tool, not global — Jane can hold a pro axe and a basic hoe.
5. **Watering can capacity** is not represented in the art. If the can empties, the design intent is that the pour frame still plays but the engine suppresses the water column and the tile does not change.
6. **Idle-with-tool** poses are not authored. Jane returns to the existing idle frames in `character.png` between actions; the tool is not drawn while idle.

---

## Regenerating the art

`gen_tools.js` depends on the shared drawing primitives and Jane's wardrobe spec that live at the top of `gen_cast.js`. It is evaluated **after** the shared part of that file, in the same scope:

```js
const cast   = await readFile('art/gen_cast.js');
const shared = cast.slice(0, cast.indexOf("await saveFile('art/character.png'"));
const tools  = await readFile('art/gen_tools.js');
// eval shared + tools together
```

This keeps Jane single-sourced: a wardrobe change in `gen_cast.js` (jacket color, hair, build) repaints all 96 tool frames on the next run. Do not copy her spec into the tool generator.

Things you are most likely to want to change, and where:

| Change | Where in `gen_tools.js` |
|---|---|
| Tier materials | `TIER` table |
| Handle color | `WOOD` |
| Tool head shapes | `HEAD` — three orientations (`up`, `diag`, `down`) per tool |
| Swing arc / grip points | `SWING` — grip `g`, head anchor `a`, orientation `o` per frame |
| Watering can pose | `CAN` — body box, spout pixels, grip, stream flag |
| Body lean and stance | `workFrame` (`lean`, `stance`) |
| Grip and arm posing | `arms` |

---

## Assets in this bundle

| File | What it is |
|---|---|
| `tool_hoe.png` | Hoe sheet, 64 × 192, ship as-is |
| `tool_can.png` | Watering can sheet, 64 × 192, ship as-is |
| `tool_axe.png` | Axe sheet, 64 × 192, ship as-is |
| `tool_pick.png` | Pickaxe sheet, 64 × 192, ship as-is |
| `gen_tools.js` | Generator — source of truth for the four sheets |
| `tools_review.png` | All 4 tools × 3 tiers × 2 facings × 4 frames, 5× zoom, for eyeballing |
| `tools_twohand.png` | 14× zoom of representative rows, for checking the grip |
| `The Haunt - Tools and Work Animations.dc.html` | Review page (design reference, not production code) |

The review page loads the sheets from `art/`; keep that relative path if you open it inside the project.

## Screenshots

Not included. Ask if static screenshots of the review page would help.
