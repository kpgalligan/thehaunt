# Handoff: The Haunt — town exterior art pipeline

## Overview

This bundle contains the visual system for **The Haunt** (Godot 4 / C#, `kpgalligan/thehaunt`) and a complete set of palette-correct placeholder art for the **town exterior** map.

Two things are being handed off:

1. **Production-ready art assets** — 55 terrain tiles, two building facades, props, lighting sprites and a character walk sheet, as native-16px PNG atlases plus a Godot 4 `TileSet` resource. These are real assets, meant to be imported and used.
2. **The art direction spec** the assets were authored against — palette, projection, tile grammar, lighting model, seasons, and how visual dread escalates by act. This governs all future art, bought or commissioned.

The work to do: import the assets, replace the procedural placeholder rendering in the town map with them, wire the lighting tint to the existing clock, and wire the store's open/closed facade to `ShopHours`.

## About the design files

Unlike a typical UI handoff, **the PNGs in `art/` are the deliverable, not a reference** — import them as-is. Do not redraw them, do not scale them, do not run them through a filter.

The two files in `reference/` are HTML documents, and those *are* reference only:

- `The Haunt - Art Direction.dc.html` — the art direction bible. Read this first. It is the spec; the assets are one implementation of it.
- `The Haunt - Placeholder Art Kit.dc.html` — an asset sheet showing every atlas, the animated walk cycle, and the assembled scene in day and dusk.

Open either in a browser. They need `reference/support.js` and `reference/doc-page.js` alongside them (both are included).

## Fidelity

**High fidelity, with one exception.**

The palette, tile grammar, lighting values, sprite dimensions and atlas layouts are final — treat every number in this document as exact. The *artwork itself* is deliberate placeholder quality: the buildings are plain and the character walk is serviceable rather than good. It is palette-correct and structurally correct, so commissioned art drops in beside it without a re-lay.

Do not "improve" the art in code. If something looks wrong, it is either an import setting (see below) or a note for the artist.

## Assets

All under `art/`. Every pixel is drawn from the 30-color palette below; there are zero off-palette colors.

| File | Size | Contents |
| --- | --- | --- |
| `terrain.png` | 256×64 | 55 terrain tiles, 16×16 grid, 16 cols × 4 rows |
| `building_townhall.png` | 128×128 | Town hall facade, 8×8 tiles |
| `building_store.png` | 224×96 | General store, 7×6 tiles — open variant at x0, closed at x112 |
| `props.png` | 256×64 | Well, 2 benches, notice board, 3 planters, lamp post, 3 window states |
| `lights.png` | 160×64 | Radial falloff sprites + 4-frame flame strip |
| `character.png` | 96×96 | 6 frames × 3 directions, 16×32 cells |
| `thehaunt_terrain.tres` | — | Godot 4 TileSet, all 55 tiles registered, physics layer with collision on woods tiles |
| `scene_day.png` | 480×270 | Reference composite — target look at midday |
| `scene_dusk.png` | 480×270 | Reference composite — target look at 18:00 |

The two `scene_*.png` files are **reference images, not game assets**. They show the assembled result you are aiming at. Compare your in-engine screenshot against them.

### Import settings

Every PNG must be imported with **Filter: Nearest** and **Mipmaps: off**. A single wrong filter setting is the difference between pixel art and mush. This is the most common way this handoff gets broken.

## Engine-side changes

### 1. Resolution and viewport

Currently 640×360. **Change to 480×270.**

640×360 shows 40×22 tiles, which makes the 48×30 town almost entirely visible at once and renders characters unreadably small. 480×270 shows 30×17 tiles — roughly double the apparent size of every character and building.

- Keep the window at 1280×720 (2.66× scale) or use 960×540 for a clean 2×.
- `TileSize = 16` stays. Tile centers stay at `(x*16+8, y*16+8)`.
- Camera limits will need a re-check against the new viewport.

### 2. Character sprite dimensions

Currently 16×22. **Change to 16×32.**

- Feet anchored to the bottom row of the cell; the sprite occupies one tile of floor and overhangs one tile upward.
- The existing facing encoding is unchanged: `0=down, 1=left, 2=right, 3=up`.
- **Right is a horizontal flip of left.** The sheet holds three directions only; mirror row 1 for facing 2.

`character.png` layout — 16×32 cells, origin top-left:

| | col 0 | col 1 | col 2 | col 3 | col 4 | col 5 |
| --- | --- | --- | --- | --- | --- | --- |
| **row 0** (y=0, down) | idle A | idle B | walk 1 | walk 2 | walk 3 | walk 4 |
| **row 1** (y=32, left) | idle A | idle B | walk 1 | walk 2 | walk 3 | walk 4 |
| **row 2** (y=64, up) | idle A | idle B | walk 1 | walk 2 | walk 3 | walk 4 |

Animation timings:

- **Idle** — 2 frames (cols 0–1) at 1.5fps.
- **Walk** — 4 frames (cols 2–5) at 8fps. The cycle is contact / pass / contact / pass; feet are planted and the body carries a 1px bob on the pass frames. Both are already baked into the frames — play them at a flat 8fps, do not add engine-side bobbing.

### 3. Terrain tiles

Assign `art/thehaunt_terrain.tres` to a `TileMapLayer`. All 55 tiles are registered at 16×16 with a physics layer; box collision is already present on the woods tiles, so the map boundary is solid with no code.

Atlas coordinates are `(column, row)` in tile units.

**Row 0 — ground**

| Col | Tile | Col | Tile |
| --- | --- | --- | --- |
| 0–3 | `grass_a`–`grass_d` (base) | 10 | `rut_h` (wheel rut, horizontal) |
| 4 | `grass_clover` | 11 | `rut_v` (wheel rut, vertical) |
| 5 | `grass_stones` | 12–13 | `gravel_a`, `gravel_b` |
| 6 | `grass_bare` | 14–15 | `woods_a`, `woods_b` — **blocking** |
| 7–9 | `dirt_a`–`dirt_c` | | |

Grass detail tiles (cols 4–6) go in at **≤8% frequency and never adjacent to each other**. The reference scene uses 3% clover / 2% stones; anything much higher reads as noise.

**Row 1 — dirt-over-grass autotile, 16 configurations**

Named by which sides retain grass: `dirt_iso` (0), `dirt_n` (1), `dirt_e` (2), `dirt_s` (3), `dirt_w` (4), `dirt_ne` (5), `dirt_se` (6), `dirt_sw` (7), `dirt_nw` (8), `dirt_c` (9, fully surrounded by dirt), `dirt_ns` (10), `dirt_ew` (11), `dirt_new` (12), `dirt_sew` (13), `dirt_nsw` (14), `dirt_nse` (15).

Edges are irregular by 2–4px — roads are worn, not cut.

**Manual step:** the `.tres` registers these tiles but does not define a terrain set. In the TileSet inspector, add a terrain set and paint the 16 row-1 tiles with their matching peering bits. This takes about two minutes in the editor and is far easier there than in the resource file. Alternatively, index them directly by a 4-bit neighbour bitmask using the order above.

**Row 2 — plaza**

| Col | Tile | Col | Tile |
| --- | --- | --- | --- |
| 0–1 | `cobble_a`, `cobble_b` | 8 | `kerb_se` |
| 2 | `cobble_worn` (the plaza's centre stone) | 9 | `kerb_sw` |
| 3 | `kerb_n` | 10 | `kerb_nw` |
| 4 | `kerb_e` | 11 | `woods_c` — **blocking** |
| 5 | `kerb_s` | 12–15 | `dirt_inner_se/sw/nw/ne` |
| 6 | `kerb_w` | | |

Kerb naming follows the autotile convention: `kerb_n` has non-cobble to the north. The plaza gets a hard 1px `stone-dark` kerb — it is the one built surface in town.

`cobble_worn` is placed **once**, at the plaza centre. It is an Act I dread tell (see below); it is never pointed at and never repeated.

**Row 3 — boundary**

| Col | Tile |
| --- | --- |
| 0–2 | `woods_d`, `woods_e`, `woods_f` — **blocking** |
| 3–6 | `woods_cnr_se`, `woods_cnr_sw`, `woods_cnr_nw`, `woods_cnr_ne` — **blocking** |

**Replace the existing stone border ring with these.** The map limit should read as forest that turns you around, not as a wall — the no-leave rule deserves to be diegetic from the first hour.

### 4. Buildings

Both facades are drawn **taller than their collision footprint** and overhang upward. This is the elevation rule from the spec: ground is drawn flat, anything vertical is drawn front-face-only as though the camera sits slightly above and dead ahead. No side walls, ever.

Neither building requires moving a single collision cell from the current map.

**Town hall** — `building_townhall.png`, 128×128 (8×8 tiles)

- Footprint `x20–27, y6–11` (8 wide, 6 rows). Draw the sprite with its **bottom row aligned to map row 11**; the top 2 rows overhang above the footprint.
- Bands bottom-to-top: foundation (1), wall (3), eave (1), roof (2), ridge + cupola (1).
- Door at `(23,11)`, unchanged — 1 tile of collision, drawn 2 tiles tall with a lit fanlight above.
- Tallest structure in town. The mayor's authority is architectural; keep it reading as dominant over the store.

**General store** — `building_store.png`, 224×96 (two 7×6 variants)

- Footprint `x8–14, y8–11` (7 wide, 4 rows). Bottom row of the sprite aligns to map row 11; top 2 rows overhang.
- **Open variant** at source x=0, **closed variant** at source x=112. Each is 112×96.
- Door at `(11,11)`, unchanged.
- Drive the variant from `ShopHours`: open shows a warm interior glow through the doorway and lit windows; closed shows a shuttered door and shuttered windows. This is the cheapest possible "is the store open" affordance, and it means the player never has to read the sign twice.
- The hanging sign on its bracket is part of the facade. A 2-frame idle sway exists as an **Act II** tell — do not animate it in Act I.

### 5. Props

`props.png`, source rectangles in pixels:

| Prop | Source rect | Tiles |
| --- | --- | --- |
| Well | `(0, 0, 32, 32)` | 2×2 |
| Bench A | `(32, 16, 32, 16)` | 2×1 |
| Bench B | `(64, 16, 32, 16)` | 2×1 |
| Notice board | `(96, 0, 32, 32)` | 2×2 |
| Planters ×3 | `(128, 16, 16, 16)`, `(144, 16, …)`, `(160, 16, …)` | 1×1 |
| Lamp post | `(176, 16, 16, 48)` | 1×3 |
| Window lit | `(208, 0, 16, 16)` | 1×1 |
| Window dark | `(224, 0, 16, 16)` | 1×1 |
| Window shuttered | `(240, 0, 16, 16)` | 1×1 |

The plaza at `x22–26, y18–21` currently has nothing in it. It is the town's social room; dressing it is the highest-value change in this batch.

**Build the window states as a swappable layer, addressable per building, from the start.** Almost every dread beat in the act escalation is a window state change. Baking window pixels into wall tiles will cost a re-draw later.

### 6. Lighting

One full-screen tint layer over the world, below the UI, driven off the existing `TenMinuteTicked` signal and lerped between these keys. All art is authored at the **day** values; every other hour is this one multiply.

| Time | Tint | Alpha | Blend | Notes |
| --- | --- | --- | --- | --- |
| 06:00 dawn | `#d8a878` | 22% | overlay | low sun |
| 09:00–16:00 day | — | 0% | — | the reference state |
| 18:00 dusk | `#c4703f` | 30% | multiply | lanterns light here |
| 20:00 evening | `#3f4d7a` | 42% | multiply | blue hour, windows warm |
| 23:00 night | `#232a4a` | 58% | multiply | navigable by lantern only |
| 01:00–01:59 late | `#1b1e33` | 66% | multiply | the clock's clamp hour; push saturation to 0 |

`scene_dusk.png` was composited at `#c4703f` / 44% multiply, slightly stronger than the 30% spec, to make the lighting legible in a still. **Use 30% in engine.**

**Interiors do not receive the tint.** They get their own fixed warm key. That contrast is what makes stepping inside at dusk feel like relief.

**Point lights.** Lanterns, lit windows and the store's open door punch through the tint as **additive** radial sprites from `lights.png`:

| Sprite | Source rect |
| --- | --- |
| Falloff, 2-tile radius | `(0, 16, 32, 32)` |
| Falloff, 4-tile radius | `(32, 0, 64, 64)` |
| Flame, 4 frames | `(96, 0, 16, 16)` … `(144, 0, 16, 16)` |

- Two falloff sizes only. The falloff is a **hand-dithered sprite, not a shader gradient** — keep it pixel-honest.
- Flame loop: 4 frames at 6fps, with ±4% radius variation on the light it casts.
- **Nothing in this town is lit by anything but fire.** There is no electric light in the palette. That is characterisation, not an oversight.

## Design tokens — the palette

Thirty colors, two slots reserved. Every asset in the game draws from this list and nothing else. Ramps are five steps: two shadow, one base, two light. **Never introduce a sixth step** — dither between two instead.

The greens, earths and stones extend the hexes already present in `TownMap.cs` and `PlaceholderSprites.cs`, so existing placeholder maps do not clash with finished art during the transition.

**Ink** — outlines, text, deepest shadow
`ink-900 #171310` · `ink-700 #2b241d` · `ink-500 #453a2e` · `cream #ede3cb` · `stone-pale #b8b5a5`

**Green** — grass, foliage, crops
`green-dark #2f5228` · `green-mid #457539` · `green-base #4a7c3a` · `green-light #5f9445` · `green-pale #86ad5c`

**Earth** — road, tilled soil, timber
`earth-dark #4a3526` · `wood-warm #6b4a2f` · `earth-mid #7a5b3c` · `earth-base #8a6a45` · `earth-light #a5855c`

**Stone** — masonry, cobble, slate roofs
`stone-dark #3e4241` · `stone-shade #575a58` · `stone-base #7a7a7a` · `stone-light #9a9a8a` · `barn-red #a4432f`

**Sky, water, flesh**
`sky-day #8fb8cf` · `water-mid #47788c` · `water-deep #2e5566` · `skin-base #e8c8a0` · `skin-shade #c49a72`

**Accents** — rationed, never terrain
`lantern #f2b95c` · `hair-stock #5a4a3a` · `plum #6b4560` · `bile-green #7d8f4a` · `bone #cfd6d1`

`plum`, `bile-green` and `bone` are the **dread accents**. In Act I they appear nowhere in the town exterior. Spending them early spends the whole effect.

## Rendering rules

These govern any new art, in any tileset:

- **Projection: flat-top three-quarter.** Ground drawn straight down, no perspective skew. Vertical things drawn in elevation, front face only. No side walls.
- **Light from the upper left, always.** Highlights on top and left edges; a 1px `ink-700` contact shadow along the bottom-right. Never a drop shadow larger than 2px.
- **Outlines: selective, never black.** Terrain has no outline. Objects a player can walk behind get a 1px `ink-700` outline on their lower half only. Characters are fully outlined in `ink-700` — that outline is what makes them pop off any terrain, and it is the one place the rule is absolute.

## Act escalation — plan for this now

The art **never leads the writing, it confirms it.** A player should notice something is wrong only after an NPC has already made them uneasy, then feel the town has been like this all along.

Mechanically, each act is a **variant set of the same tiles at the same coordinates, swapped by a story flag**. No map is ever rebuilt. Budget roughly 20% extra tiles per act, not a second tileset. Structure the tile lookup so a variant swap is a flag check, not a re-lay — doing this now is nearly free and expensive to retrofit.

- **Act I — nothing is wrong.** Full warm palette. No plum, bile-green or bone anywhere. One exception per map: in town, `cobble_worn` at the plaza centre is a slightly wrong shape for a paving stone. Plus one piece of level design — the road always curves out of frame to the west, so the player never sees a horizon, only more town.
- **Act II — the details stop agreeing.** Greens lose 8% saturation. Plum enters the shadow step of every stone ramp, so masonry shadows go faintly violet. Four tells, all window and prop swaps: one house window lit at 01:00 every night, a different house each week; one building's contact shadow falling upper-left, against the light; the store's sign gaining a 2-frame sway with no wind anywhere else on screen; autumn leaf litter drifting toward the plaza centre regardless of which way the player walks.
- **Act III — the town admits it.** Palette splits: highlights push hotter toward `lantern`, shadows collapse into plum-black, mid-tones thin out — a high-contrast, low-mid image that is tiring to look at, deliberately. `green-dark` becomes the dominant grass. Bile-green appears in living vegetation. Ash gathers in the cobble seams. Windows go shuttered building by building. On tribute nights the lantern flames burn `bone` instead of amber — one recolored 4-frame sprite, and the most legible signal in the game that tonight is different.

Rule of thumb for anything proposed later: if it would make a player say "that's scary," it is Act III. If it makes them look twice and move on, it is Act II. Act I gets nothing.

## Keep the procedural placeholders working

Do not delete `PlaceholderSprites` or the procedural tile atlases. They cost nothing to keep, they let a new map ship before its art exists, and every asset that replaces one of them is a visible piece of progress.

## Not yet drawn

Farm and interior tilesets, crop growth stages, tool-use animations (till, water, chop — see the frame table in the bible's §06), NPC variants, and the Act II / Act III variant sets. All inherit this palette, so they can be added in any order.

## Files in this bundle

```
art/
  terrain.png              55 tiles, 16x16 grid
  building_townhall.png    8x8 facade
  building_store.png       two 7x6 variants, open + closed
  props.png                9 props + 3 window states
  lights.png               2 falloffs + 4-frame flame strip
  character.png            6 frames x 3 directions, 16x32
  thehaunt_terrain.tres    Godot 4 TileSet
  scene_day.png            reference composite, midday
  scene_dusk.png           reference composite, 18:00

reference/
  The Haunt - Art Direction.dc.html       the full spec — read first
  The Haunt - Placeholder Art Kit.dc.html asset sheet + animated walk cycle
  support.js, doc-page.js                 needed to open the two HTML files
```

Nothing in this bundle names the town, its residents, or the malevolence. That is still the designer's.
