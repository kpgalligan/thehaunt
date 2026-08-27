# Handoff: The Haunt — Motel & Town Signage

## Overview

Two things ship here:

1. **The motel** — a new canon location. A late-1950s googie motor court: an office plus four guest rooms, each entered directly from a parking lot. All four rooms are locked by default and unlock individually via story flags. A pole sign carries the motel's name and a neon `NO VACANCY` panel whose `V` flickers.
2. **A signage system** — four sign mounting types and the rules for applying them, so every business exterior in town (stores, bars, police station) gets a sign without inventing a new visual language each time. City hall deliberately gets no exterior sign.

Target: Godot 4 / C#, `kpgalligan/thehaunt`, 16px tiles, 480×270 viewport, palette-locked pixel art.

## About the Design Files

The files in this bundle are **design references created in HTML**. `The Haunt - Motel and Signage.dc.html` is a design document — it presents mockups and specifications, it is not code to port. The PNGs in `art/` are **mockups, not production atlases**: they are single composited images showing the intended read, drawn at native pixel scale (1 image pixel = 1 game pixel).

The implementation task is to build the motel as a real Godot map in the existing codebase, following the patterns already established in `src/World/TownMap.cs` and `src/World/MapRoot.cs`, and to author the sprite atlases from the specifications below. Do not slice the mockup PNGs into an atlas — they include lighting, ground, and adjacency that belong to different layers.

## Fidelity

**High-fidelity for colour, geometry, and layout; proposal-grade for game design.**

- Every colour is exact and comes from the locked palette. Every pixel measurement below is exact.
- The motel does not exist anywhere in the repo — no file mentions it. Map ids, flag names, and tile coordinates below are proposals, named to match existing conventions (`MapIds.Town`, `MapIds.GeneralStore`).
- The motel's **name is unknown**. The pole sign has a blank nameplate panel with one ruled line where the name goes. It is blank on purpose. `docs/story/README.md` and `docs/story/cast.md` are not on `main`, so the canon name and the full location list were unavailable. Do not invent a name.

## Screens / Views

### 1. Motel exterior (`MotelMap`)

**Purpose.** Outdoor hub. The player arrives from the road, can always enter the office, and can enter rooms 1–4 as the story unlocks them.

**Reference:** `art/scene_motel_day.png`, `art/scene_motel_night.png` (416 × 288 px = 26 × 18 tiles).

**Assumption to confirm:** this is its own map reached off the road, not a district inside the existing 48 × 30 `TownMap`. The mockup is sized as a standalone map.

#### Layout, by tile row (16 px tiles)

| Rows | Content |
| --- | --- |
| 0–2 | Grass backdrop above the buildings |
| 2–8 | Office facade (x 1–6) and room strip facade (x 7–25) |
| 9 | Concrete walkway, x 6–26. Curb along its south edge |
| 10–15 | Asphalt parking lot, x 6–26. Eight painted stalls |
| 16–17 | Dirt road, full width. Continuous with the town/farm road grammar |

Grass, asphalt, concrete, and dirt are all **mottled**, not flat: per-pixel hash picking between 3–4 palette values, matching the speckle approach already in `TownMap.BuildAtlasTexture`. Rough mix ratios — grass 6% `#457539`, 6% `#5f9445`, 3% `#2f5228`, remainder `#4a7c3a`; asphalt 8% `#3e4241`, 8% `#7a7a7a`, remainder `#575a58`; concrete 7% `#b8b5a5`, 5% `#7a7a7a`, remainder `#9a9a8a`; dirt 8% `#7a5b3c`, 8% `#a5855c`, remainder `#8a6a45`.

#### Office (pixel coords, origin top-left of the 416 × 288 frame)

| Element | Rect (x, y, w, h) | Colour |
| --- | --- | --- |
| Outline | 14, 38, 84, 108 | `#2b241d` |
| Enamel wall | 16, 40, 80, 104 | `#ede3cb`, 4% speckle `#b8b5a5` |
| Roof gravel band | 16, 40, 80, 8 | `#575a58` |
| Upswept eave (overhangs left to x=6) | 6, 44, 92, 4 | `#2b241d` |
| Aqua eave stripe | 6, 48, 92, 4 | `#5fb9b0` |
| Eave tip | 6, 52, 10, 2 | `#2b241d` |
| OFFICE sign box | 22, 58, 52, 12 | `#171310`, letters `#5fb9b0` day / `#f2b95c` night |
| Plate glass frame | 22, 76, 48, 42 | `#2b241d` |
| Plate glass | 23, 77, 46, 40 | `#2e5566` day, `#2a2a20` night unlit, `#f2b95c` night lit |
| Mullions | vertical at x=46, horizontal at y=90 | `#b8b5a5` |
| Glass reflection (day only) | 18 stepped 4×1 bars from (26,113) up-right | `#5c8fa3` |
| Sill | 22, 118, 48, 2 | `#ede3cb` |
| Door frame / door | 76, 100, 16, 40 / 77, 101, 14, 38 | `#2b241d` / `#a4432f` |
| Door knob | 88, 118, 2, 3 | `#171310` |
| Soda machine | 2, 112, 10, 28 (frame `#2b241d`, body `#a4432f`, panel 4,116,6,5 `#ede3cb`) | |
| Kick plate | 16, 140, 80, 4 | `#3e4241` |

The eave overhanging past the wall on the left is the single strongest era cue. Keep it.

#### Room strip

Outline `14`-style: `#2b241d` at (110, 62, 292, 84). Enamel wall `#ede3cb` at (112, 64, 288, 80) with 4% `#b8b5a5` speckle.

| Element | Rect | Colour |
| --- | --- | --- |
| Roof gravel | 112, 64, 288, 10 | `#575a58` |
| Shadow line | 112, 74, 288, 2 | `#2b241d` |
| Googie stripe | 112, 76, 288, 4 | `#5fb9b0` |
| Kick plate | 112, 140, 288, 4 | `#3e4241` |

Four identical room units at `ux = 112 + i * 68`, i = 0..3. Within each unit:

| Element | Rect (relative to `ux`) | Colour |
| --- | --- | --- |
| Door frame | +4, 100, 18, 40 | `#2b241d` |
| Door | +5, 101, 16, 38 | alternating `#a4432f`, `#5fb9b0`, `#a4432f`, `#5fb9b0` |
| Knob | +17, 118, 2, 3 | `#171310` |
| Number plaque | +8, 90, 10, 8 | `#171310`, digit `#ede3cb`, 3×5 font at 1× |
| Window frame | +28, 94, 32, 28 | `#2b241d` |
| Glass | +29, 95, 30, 26 | `#2e5566` day, `#171310` night unlit, `#f2b95c` night lit |
| Mullions | vertical +43, horizontal at y=107 | `#b8b5a5` |
| Sill | +28, 122, 32, 2 | `#ede3cb` |
| Canopy post | +2, 80, 2, 62 | `#5fb9b0` |

**Ice/vending alcove** at the strip's east end: frame `#2b241d` (372, 96, 24, 44), recess `#575a58` (373, 97, 22, 42), machines `#5fb9b0` (376, 100, 7, 16) and `#a4432f` (386, 100, 7, 16), the word `ICE` centred at (384, 122) in `#ede3cb`. It is a cheap ambient night light and a natural place to hide an item.

#### Parking lot details

- Curb: `#b8b5a5`, (96, 158, 320, 2).
- Stall stripes: `#b8b5a5`, 2 × 40 px, at x = 104, 140, 176, 212, 248, 284, 320, 356, y = 170. Faded, not white.
- Asphalt cracks: ~40 short horizontal runs of `#3e4241`, 1–7 px long, scattered across (100–410, 164–252).
- Lot entrance: a dirt apron (132, 250, 64, 8) breaking the grass between lot and road.
- Road wear: `#7a5b3c` bars, 12 × 2 px, every 24 px along y = 271.

#### Pole sign

Stands in the grass apron between the lot entrance and the road, west of the entrance.

| Element | Rect | Colour |
| --- | --- | --- |
| Pylon outline / body | 50, 228, 10, 34 / 51, 228, 8, 34 | `#2b241d` / `#575a58` |
| Concrete foot | 44, 258, 22, 6 | `#3e4241` |
| Cabinet outline | 18, 168, 74, 62 | `#2b241d` |
| Cabinet face | 20, 170, 70, 58 | `#ede3cb` |
| Atomic starburst | vertical 80,164,2,10 + horizontal 76,168,10,2 | `#5fb9b0` |
| MOTEL panel | 22, 172, 66, 16 | `#a4432f`, letters `#ede3cb` at 2× |
| Nameplate (blank) | 22, 190, 66, 14 | `#171310`, one rule 26,196,58,2 `#453a2e` |
| Vacancy panel | 22, 206, 66, 20 | see below |
| Bulb rail | 2 × 2 px every 6 px from x=22 to 88, y=224 | `#b8b5a5` day / `#f2b95c` night |

### 2. The vacancy sign

**Reference:** `art/motel_sign_states.png` — four states, left to right: day unlit, night V on, night V off, night full.

Neon is bent glass and charged per letter, so the motel bought **one** panel. The tubes read `NO VACANCY` permanently. `NO` is simply not switched on, which is nearly every night. This is the sign's whole characterisation: the place is not doing well.

Panel is 66 × 20 px on `#171310`. Two centred lines of the 3×5 pixel font at 1×:

- `NO` at panel y + 3
- `VACANCY` at panel y + 11

Three independent circuits:

| Circuit | Contents | Behaviour |
| --- | --- | --- |
| A | `NO` | Off unless the motel is full. Effectively never lit in act one. |
| B | `ACANCY` | On from dusk to dawn, steady. |
| C | `V` | On the same feed as B through a failing transformer. Blinks. |

Colours:

| Tube state | Hex |
| --- | --- |
| Lit neon | `#e05a3f` |
| Unlit glass, night | `#63403a` |
| Unlit glass, day | `#6d4038` |

The unlit-glass colour matters — the `NO` must stay faintly visible at night so the player can see the tube exists. It is not simply absent.

**Blink timing.** 4.0 s cycle. `V` off for 0.55 s, on for 3.45 s. Hard cut both directions, no fade, no easing. Do not randomise it; the regularity is what makes it read as broken rather than atmospheric.

**Glow.** A baked additive sprite, not a shader: radial gradient `rgba(224, 90, 63, 0.30)` → transparent, radius ~54 px, centred on the panel. It turns off with circuit B and C together. The `V`'s glow cuts with the `V`.

### 3. Sign mounting types

**Reference:** `art/signs_mounts.png` — four types, day row on top, night row below.

Every business exterior in town uses exactly one of these four. The mount does the period work, so the lettering can stay plain.

| Mount | Construction | Assigned to |
| --- | --- | --- |
| **Pole** | Cabinet on a pylon set back from the road, meant to be read from a moving car. The most expensive-looking mount in town. | The motel. Anything else expecting passing traffic. |
| **Wall band** | Flush letters on a dark band above the door, lit from below. | Civic and institutional: police station, clinic — anything the town paid for. |
| **Hanging bracket** | Perpendicular to the facade on an iron arm, one bulb above it. The only mount readable side-on. | Bars, and anything on a walkable street. |
| **Window** | Band sign above the glass plus a small neon word inside it. | Stores. The neon word doubles as the open/closed state, so it reads as a shop-hours tell. |

**City hall gets no exterior sign.** Confirmed with the client. It reads as civic because it is the only masonry facade in town, which is stronger than lettering. Civic signalling is a later pass.

## Interactions & Behavior

### Doors and locks

Five doors on one facade row (y = 8). All rooms locked by default; the office is the only one open at first contact.

| Door | Tile | Map id | Unlock flag |
| --- | --- | --- | --- |
| Office | (5, 8) | `motel_office` | none — always open |
| Room 1 | (7, 8) | `motel_room_1` | `motel_room1_open` |
| Room 2 | (12, 8) | `motel_room_2` | `motel_room2_open` |
| Room 3 | (16, 8) | `motel_room_3` | `motel_room3_open` |
| Room 4 | (20, 8) | `motel_room_4` | `motel_room4_open` |

Each room is its own map id and its own flag, so access can be granted in any order the story wants. Rooms are not a single map with a variant parameter.

**Locked doors must not fail silently.** Use the existing `Door` node with an `IsEnabled` predicate; when it evaluates false, the interaction prompt still appears and interacting returns a line rather than doing nothing. A player rattling four locked handles in a row should feel watched, which only works if the game answers each time.

Spawn markers: `from_road` on the road apron, and one `from_<room>` marker per door, placed one tile south of the door on the walkway (row 9), matching the existing spawn-clearance rule in `TownMap.BuildSpawns`.

### Day / night

Day and night are **two sprites per sign, not a tint** — unlit day, lit night, identical footprint. Swap on time of day. The light pool underneath is a separate additive sprite so it can be reused across signs.

At night the mockup applies an overall `rgba(24, 30, 72, 0.60)` wash, then redraws lit elements above it at full colour, then adds additive light pools:

| Source | Centre | Radius | Colour |
| --- | --- | --- | --- |
| Office lobby | 46, 120 | 60 | `rgba(242,185,92,0.50)` |
| Occupied room window | 268, 116 | 42 | `rgba(242,185,92,0.42)` |
| Pole sign | 55, 200 | 64 | `rgba(224,90,63,0.34)` |
| Canopy stripe, west | 124, 150 | 30 | `rgba(95,185,176,0.25)` |
| Ice alcove | 384, 120 | 26 | `rgba(95,185,176,0.30)` |

### Occupancy tell

At night the lobby is lit, **one** guest room is lit, three are dark. In the mockup it is room 3. Which room is lit must be driven by a story flag, not decoration — the occupancy read lands before any dialogue does.

### Animation budget

**Nothing in the town animates except the `V`.** One flickering element in the whole game. The moment a second thing flickers, the first stops meaning anything. If a later act needs a second animated sign, it should replace this one, not join it.

## State Management

| State | Type | Purpose |
| --- | --- | --- |
| `motel_room1_open` … `motel_room4_open` | bool story flags | Gate each room's `Door.IsEnabled` |
| `motel_room_lit` | int or enum, 1–4 | Which room shows a lit window at night |
| `motel_full` | bool | Lights circuit A (`NO`). Expected false for all of act one |
| time of day | existing | Selects day vs night sign sprites and the light-pool layer |

No data fetching. All state is local to the save.

## Design Tokens

### Palette — existing (from the Art Direction Bible)

| Token | Hex | Used for |
| --- | --- | --- |
| ink-900 | `#171310` | Sign backgrounds, outlines, deepest shadow |
| ink-700 | `#2b241d` | Building outlines, frames |
| ink-500 | `#453a2e` | Nameplate rule |
| cream | `#ede3cb` | Enamel wall panels, sign lettering, sills |
| stone-pale | `#b8b5a5` | Enamel chalking speckle, mullions, curb, stall stripes |
| green-dark / mid / base / light | `#2f5228` / `#457539` / `#4a7c3a` / `#5f9445` | Grass mottle |
| earth-mid / base / light | `#7a5b3c` / `#8a6a45` / `#a5855c` | Dirt road mottle |
| stone-dark | `#3e4241` | Asphalt cracks, kick plates, concrete foot |
| stone-shade | `#575a58` | Asphalt base, roof gravel, pylon |
| stone-base | `#7a7a7a` | Asphalt and concrete speckle |
| stone-light | `#9a9a8a` | Concrete walkway base |
| barn-red | `#a4432f` | MOTEL panel, doors, vending |
| water-deep | `#2e5566` | Daytime glass |
| lamp | `#f2b95c` | Incandescent bulbs, lit windows |

### Palette — the two reserved slots, now spent

| Token | Hex | Used for |
| --- | --- | --- |
| neon-aqua | `#5fb9b0` | Googie stripes, canopy posts, aqua doors, aqua neon lettering |
| neon-red | `#e05a3f` | Lit neon tube |

Two lit colours only: neon red and aqua. Incandescent bulbs use the existing lamp amber. Nothing else in the game glows.

Derived, non-palette values used only for unlit glass and one reflection: `#63403a` (unlit tube at night), `#6d4038` (unlit tube in daylight), `#5c8fa3` (day glass reflection), `#2a2a20` (unlit office glass at night).

### Typography — the 3×5 pixel alphabet

Every sign in the game uses one 3-wide, 5-tall glyph set, at 1× for small signs and 2× for the MOTEL panel. No second typeface. It stays readable at 480×270 and makes new signs nearly free to author.

Advance is 4 px per character at 1× (3 px glyph + 1 px gap); at scale `s`, `4s`. String width = `len * 4 * s - s`.

Glyph rows, MSB left, as `row0,row1,row2,row3,row4`:

```
A 010,101,111,101,101   N 101,111,111,111,101   0 111,101,101,101,111
B 110,101,110,101,110   O 010,101,101,101,010   1 010,110,010,010,111
C 011,100,100,100,011   P 110,101,110,100,100   2 110,001,010,100,111
D 110,101,101,101,110   Q 010,101,101,110,011   3 110,001,010,001,110
E 111,100,110,100,111   R 110,101,110,101,101   4 101,101,111,001,001
F 111,100,110,100,100   S 011,100,010,001,110   5 111,100,110,001,110
G 011,100,101,101,011   T 111,010,010,010,010   6 011,100,110,101,010
H 101,101,111,101,101   U 101,101,101,101,011   7 111,001,010,010,010
I 111,010,010,010,111   V 101,101,101,101,010   8 010,101,010,101,010
J 001,001,001,101,010   W 101,101,111,111,101   9 010,101,011,001,110
K 101,101,110,101,101   X 101,101,010,101,101   - 000,000,111,000,000
L 100,100,100,100,111   Y 101,101,010,010,010   . 000,000,000,000,010
M 101,111,111,101,101   Z 111,001,010,100,111   ' 010,010,000,000,000
```

### Grid

| Value | Size |
| --- | --- |
| Tile | 16 × 16 px |
| Character | 16 × 32 px |
| Viewport | 480 × 270 |
| Motel map | 26 × 18 tiles (416 × 288 px) |

## Assets

All four PNGs in `art/` were authored for this pass by drawing to a canvas at native pixel scale — no photographic or third-party source, nothing traced. They are palette-locked to the table above.

| File | Size | What it is |
| --- | --- | --- |
| `art/scene_motel_day.png` | 416 × 288 | Full motel lot, daylight. The layout reference. |
| `art/scene_motel_night.png` | 416 × 288 | Same lot at night: lit lobby, one lit room, lit sign, light pools. |
| `art/motel_sign_states.png` | 376 × 118 | Pole sign in four states: day unlit, night V on, night V off, night full. |
| `art/signs_mounts.png` | 440 × 200 | Four mounting types, day row and night row. |

These are composites for review. Production needs the elements split across ground / obstacle / facade / sign / light layers, which is a separate atlas pass to be done after sign-off on the read.

## Files

| File | Contents |
| --- | --- |
| `The Haunt - Motel and Signage.dc.html` | The design document — mockups, specs, the live blinking-`V` demo, and the signage rules. Open it in a browser. |
| `support.js` | Runtime required by the HTML document. Keep it beside the HTML. |
| `art/*.png` | The four mockups listed above. |

Repository context this was built against: `src/World/TownMap.cs` (tile grammar, road rows 14–15, `Door` / `Sign` / `MapExit` node usage, speckled procedural atlas), `src/World/MapRoot.cs`, and the palette from the project's Art Direction Bible.

## Open questions for the client

1. **Motel name.** The nameplate is blank. `docs/story/README.md` and `docs/story/cast.md` are not on `main` and no file in the repo mentions a motel.
2. **Full location list.** Same blocker. The per-location signs (which stores, which bars, the police station's name) are unauthored until those docs are pushed.
3. **Own map, or part of town?** The mockup assumes the motel is its own 26 × 18 map reached off the road, rather than a district inside the existing 48 × 30 `TownMap`.
4. **Interiors.** Not drawn. Five are needed: the office, plus four rooms — three can share one shell with swapped furniture, one should differ.
