# Handoff 02: farm, barn, and interiors

**This is an incremental handoff.** It assumes handoff 01 (`design_handoff_town_art`) is already implemented: the palette is in use, 16px tiles are confirmed, the viewport is 480×270, characters are 16×32, and `art/terrain.png` + `thehaunt_terrain.tres` are wired up.

Nothing here changes any of that. If a rule in handoff 01 conflicts with something below, handoff 01 wins — except for the one documented exception in §3 (the Crops atlas cell size).

## What's new

| Area | Change |
| --- | --- |
| Farm soil | 2 flat tiles → 36 tiles: dry and wet autotile sets plus a furrow direction set |
| Crops | 20 sprites, 4 crops × 5 stages, drawn at **16×32** |
| Barn | New structure, 3 repair states — **a proposal; no barn exists in the codebase** |
| Farm exterior | Farmhouse facade, 2 trees, shipping bin, fences, gates, paths, pond banks |
| Interiors | 64-tile interior atlas + 34 furniture pieces, replacing the 4 procedural colors per room |
| Rooms | 6 laid out: 3 existing at real coordinates, 3 proposed |

Three new Godot TileSet resources are included and ready to assign.

## Files

```
art/
  farm_terrain.png       256×64   64 tiles
  crops.png               80×128  20 sprites @ 16×32
  barn.png               288×112  3 states @ 6×7 tiles
  farm_buildings.png     256×96   farmhouse, 2 trees, shipping bin
  interior.png           256×64   64 tiles
  furniture.png          256×128  34 pieces
  thehaunt_farm.tres              64 tiles, collision on rocks/stumps/fences/log
  thehaunt_crops.tres             20 tiles, 16×32 cells, texture_origin (0,8), no collision
  thehaunt_interior.tres          64 tiles, "walkable" custom data + collision

reference/            (design references only — do not ship)
  scene_farm.png       640×480  proposed farm layout on the real 40×30 grid
  room_farmhouse.png   room_store.png   room_townhall.png
  room_barn.png        room_church.png  room_neighbor.png
  The Haunt - Farm and Interiors Art Kit.dc.html   + support.js
```

Same import settings as before: **Filter: Nearest, Mipmaps: off** on every PNG.

---

## 1. Farm soil — `farm_terrain.png` rows 0–2

`FarmSoil` currently has two tiles (`tilled-dry #7a5a38`, `tilled-wet #5a4230`), so a plot reads as a rectangle of mud. The replacement keeps the same cell-state function — none / dry / wet — and adds the neighbour-config dimension.

**Row 0, cols 0–7 — bases and furrows**

| Col | Tile | Col | Tile |
| --- | --- | --- | --- |
| 0–1 | `soil_dry_a`, `soil_dry_b` | 4–5 | `furrow_dry_h`, `furrow_dry_v` |
| 2–3 | `soil_wet_a`, `soil_wet_b` | 6–7 | `furrow_wet_h`, `furrow_wet_v` |

**Rows 1 and 2 — autotile, 16 configs each.** Row 1 is dry, row 2 is wet. Column order is identical to the town dirt autotile from handoff 01, so the same bitmask helper serves both:

`iso, n, e, s, w, ne, se, sw, nw, c, ns, ew, new, sew, nsw, nse`

Named by which sides retain **grass** (not soil). `c` (col 9) is fully surrounded by soil — that is the tile a plot interior uses.

**Cell-state function, updated:**

```
FarmSoil cell for (x,y):
  no TileRecord                                    -> erase
  Kind == "tilled" && LastWateredDay == today      -> wet  (row 2)
  Kind == "tilled"                                 -> dry  (row 1)
  column = config from the 4 neighbours' soil-ness
```

Interior cells (config `c`) may use the row-0 bases or the furrow tiles instead — the row-0 tiles and the row-1/2 `c` tile are interchangeable for a fully-surrounded cell.

**Furrows are optional but cheap.** A tile hoed north–south looks different from one hoed east–west, which makes a plot worked in rows read as deliberate. Two ways to drive it:

- **Derived (no save change):** at paint time, if the tilled neighbours form a horizontal run use `furrow_*_h`, if vertical use `furrow_*_v`. Costs nothing, occasionally guesses wrong.
- **Stored:** one field on `TileRecord`. Follow the existing convention — not a bool, and unknown values preserved. Only worth it if the player should be able to choose.

**Row 3 — farm dressing**

| Col | Tile | Collision |
| --- | --- | --- |
| 0 | `fence_h` | solid |
| 1 | `fence_v` | solid |
| 2 | `fence_post` | solid |
| 3–6 | `fence_cnr_se`, `_sw`, `_nw`, `_ne` | solid |
| 7 | `gate_closed` | solid |
| 8 | `gate_open` | passable |
| 9–10 | `path_a`, `path_b` | passable |
| 11 | `pasture_c` | passable |
| 12 | `dead_grass` | passable |
| 13 | `flowers` | passable |
| 14 | `log` | solid |
| 15 | `hay_scatter` | passable |

Row 0 cols 8–15 are `pasture_a/b`, `weeds_a/b`, `rock_small`, `rock_large`, `stump`, `puddle`. `rock_large` and `stump` are solid; the rest are not. Pasture is a rougher, darker grass than the town's — use it as the farm's base rather than the town grass, so the two maps don't feel like the same field.

**Scatter frequency:** keep decorative tiles (weeds, rocks, flowers, hay) under about 5% combined. The reference layout uses 3% weeds / 1.2% rock / 0.5% each for the rest. Higher reads as confetti.

---

## 2. Farm exterior — `farm_buildings.png`

| Item | Source rect | Tiles | Notes |
| --- | --- | --- | --- |
| Farmhouse | `(0, 0, 96, 96)` | 6×6 | **New — no footprint exists in code.** Bottom 4 rows are the intended collision; top 2 overhang. Door at local col 3, bottom row. |
| Tree, leafy | `(96, 0, 48, 64)` | 3×4 | Bottom row is the trunk cell; the rest overhangs upward. Only the trunk cell should be solid. |
| Tree, bare | `(144, 0, 48, 64)` | 3×4 | Same anchoring. Use sparingly — one or two per map. |
| Shipping bin, closed | `(192, 0, 32, 16)` | 2×1 | Matches the existing `ShippingBin` interactable. |
| Shipping bin, open | `(224, 0, 32, 16)` | 2×1 | Lid up with produce visible — use when the bin is non-empty. |

The farmhouse is the one asset here that needs a design decision before use: `TestMap` has no farmhouse footprint, only a `house_door` spawn marker. Pick a 6-wide × 4-row footprint whose door cell aligns with that marker and the facade drops straight in.

### Reference layout — `reference/scene_farm.png`

A proposed arrangement of the real 40×30 farm at 640×480. Not an asset; a target.

- Farmhouse cols 4–9, bottom row 8. Barn cols 25–30, bottom row 9.
- Single track: south from the house door, then east along row 14 to the town road at rows 13–15 on the east edge.
- Dry plot cols 6–15 rows 17–22. Wet plot cols 18–25 rows 20–24. Both below the track, so the player crosses their own work on every trip to town.
- Pond cols 32–37 rows 20–26, with wet-soil banks. Fenced pasture cols 3–14 rows 25–28.
- Woods boundary on all four sides — same diegetic rule as town.

The two-plot split is deliberate: one dry and one watered means the soil-state difference is legible from anywhere on the map.

---

## 3. Crops — `crops.png` ⚠ spec change

**This is the one place that changes a decision from the phase 2 spec.**

Layout is unchanged in principle: one **row per CropDef in `CropDefs` order**, one **column per stage**, column = `StageForDay(GrowthDay)`.

| Row | Crop | Columns | Height |
| --- | --- | --- | --- |
| 0 | `turnip` | 0–4 | within tile |
| 1 | `greenbean` | 0–4 | overhangs upward |
| 2 | `potato` | 0–4 | within tile |
| 3 | `cauliflower` | 0–4 | overhangs upward |

All four crops have `StageDays.Length == 4`, so columns 0–3 are the growth stages and **column 4 is the mature column** (`StageForDay` returns `StageDays.Length` at `growthDay >= TotalDays`). Five columns per row, and every crop currently in the game fits. A future crop with a different `StageDays.Length` needs its own column count — the atlas is addressed by `(row, StageForDay(...))`, so widening it is additive.

**Cells are 16 wide × 32 tall.** Short crops occupy the lower 16px; beans and cauliflower use the upper half.

```
texture_region_size = Vector2i(16, 32)
texture_origin      = Vector2i(0, 8)      # per tile; already in thehaunt_crops.tres
```

> **Correction (2026-08-26).** This handoff originally specified `Vector2i(0, -8)`, and
> `thehaunt_crops.tres` shipped with it. The sign is wrong: Godot SUBTRACTS
> `texture_origin` from the tile's draw position, so a negative Y sinks the cell rather
> than lifting it. Measured in-engine, a mature turnip on cell (2,2) — whose rect is
> y 32..48 — drew its 32px region at y 32..64, putting the whole plant a full tile below
> the row it was planted in. `Vector2i(0, 8)` draws it at y 16..48: feet on the cell's
> bottom row, overhanging the row above, which is what the paragraph above always meant.
> The `.tres` and `FarmArtTests` now carry the positive value.

Two consequences:

1. **Draw order.** Crops must draw above FarmSoil and below Obstacles — the existing layer order already satisfies this.
2. **Overlap.** A tall crop on row Y paints into row Y−1. Either give the Crops layer Y-sorting, or keep plots clear of anything the player walks behind on the row above. In the reference layout the plots have a clear row above them for this reason.

If the overhang is more trouble than it's worth right now, the lower 16px of every cell is a complete crop on its own — ship with 16×16 regions and no origin offset, and the tall crops simply read shorter. The atlas does not need redrawing to make that choice later.

---

## 4. Barn — `barn.png`

**A proposal, not a spec.** There is no barn, animal, coop, or silo anywhere in the codebase, so everything here is a design suggestion you should feel free to overrule.

The premise: it came with the farm, it is falling down, and the player repairs it over time.

6 wide × 7 tall (96×112), bottom 5 rows intended as footprint, top 2 overhang. Board-and-batten siding, gambrel roof, hayloft door above a double main door. Three states side by side:

| State | Source x | Reads as |
| --- | --- | --- |
| 0 | `0` | **Derelict.** Holes in the roof, missing boards, one collapsed corner, one door hanging off. Interior visible as darkness through every gap. Weeds at the base, loose boards on the ground. Windows black — no glass. |
| 1 | `96` | **Weathertight.** Structurally sound, visibly unfinished: pale new patches on the roof, both doors hung, glass in the windows, siding unpainted. |
| 2 | `192` | **Restored.** Barn red with cream trim, slate roof, cupola vent, lit windows. |

**Drive it with a story flag holding three values, not a percentage.** Three states means three clean reads; a completion slider would need art for every value in between. The existing `StoryFlags` dictionary already stores ints, so `farm.barn_state` = 0/1/2 fits with no schema change.

Two notes worth keeping:

- **Restored is the only saturated red mass in the game.** Every other structure is cream, stone, or bare timber. That makes finishing the barn the most visible thing the player will have done — worth protecting by not using barn red anywhere else.
- **The derelict barn is the natural home for an Act I dread tell.** It is the one structure the player is actively invited to inspect, which makes it the cheapest possible place to hide something that was always there. Per the escalation rules in handoff 01, Act I gets at most one per map, and it is never pointed at.

---

## 5. Interiors — `interior.png` + `furniture.png`

Replaces the 4-color procedural atlas in each interior map. **No structural change is required**: keep the single-thickness wall ring, the oversized near-black `Surround` ColorRect behind Ground, the `walkable` custom data layer, and the Door-in-the-south-wall convention. Only the tile source changes.

`thehaunt_interior.tres` carries the `walkable` bool and box collision on every non-walkable tile, matching `FarmHouseMap.BuildTileSet` semantics exactly.

### Wall convention

- **North wall (y = 0):** a **row 2** tile (`cornice_*`). These have a dark band along their top edge, which reads as ceiling shadow.
- **East, west, south walls:** a **row 1** tile (`wall_*` or `wainscot_*`).
- **Openings** are drop-in replacements at the same coordinate: `window_dark`, `window_lit`, `window_shut`, `door_closed`, `door_open`.
- Put `threshold` (row 0 col 15) on the floor cell just inside a door.

Match the wall material to the building: `wall_log` for the farmhouse, `wainscot_plank` for the store, `wainscot_plaster` for homes, `wall_stone` for the town hall, `wall_plank` for the barn, `wall_plaster` for the meeting house.

### `interior.png` — 64 tiles

**Row 0 — floors** (all walkable)
`floor_plank_a`, `floor_plank_b`, `floor_plank_worn`, `floor_stone_a`, `floor_stone_b`, `floor_dirt`, `floor_hay`, `rug_a`, `rug_b`, `floor_board_a`, `floor_board_b`, `floor_check_a`, `floor_check_b`, `floor_stain`, `floor_dark`, `threshold`

Floors are intentionally low-contrast with staggered butt joints. They are the quietest surface in the game on purpose — furniture and characters carry the read, and a loud floor makes an interior look like a wall.

**Row 1 — wall lower and openings** (all solid except `door_open`)
`wall_plaster`, `wall_plank`, `wall_stone`, `wall_log`, `wainscot_plaster`, `wainscot_plank`, `wall_plaster_crack`, `wall_stone_crack`, `wall_cnr_l`, `wall_cnr_r`, `window_dark`, `window_lit`, `window_shut`, `door_closed`, `door_open`, `wall_beam`

**Row 2 — wall upper, ceiling, rafters** (all solid)
`cornice_plaster`, `cornice_plank`, `cornice_stone`, `cornice_log`, `ceiling_a`, `ceiling_b`, `rafter_h`, `rafter_v`, `hayloft_edge`, `wall_rail`, `upper_plaster`, `upper_plank`, `upper_stone`, `upper_log`, `plaque`, `lantern_bracket`

**Row 3 — fixtures and storage** (all solid except `cobweb`)
`stair_up`, `stair_down`, `hearth_l`, `hearth_c`, `hearth_r`, `hearth_fire`, `counter_l`, `counter_c`, `counter_r`, `shelf_empty`, `shelf_full`, `barrel`, `crate`, `sack`, `hay_bale`, `cobweb`

Hearths assemble as `hearth_l` + `hearth_c` + `hearth_r` on the north wall with `hearth_fire` below the centre. Counters assemble the same way. The lit hearth is what makes stepping inside at dusk feel like relief — per the lighting rules in handoff 01, interiors take a fixed warm key and never the outdoor tint.

### `furniture.png` — 34 pieces

Drawn to **stand on** their anchor cell: a 16×32 piece occupies one floor tile and overhangs one tile upward, same rule as the exterior buildings. Blit at `(x*16, y*16 - (height - 16))`.

**16×32 uprights** — `bed (0,0)`, `stove (16,0)`, `cupboard (32,0)`, `ladder (48,0)`, `stall (64,0)`, `candles (80,0)`, `dresser (96,0)`, `banner (112,0)`, `tallshelf (128,0)`, `stained (144,0)`

**16×16 smalls, y=0** — `cradle (160,0)`, `lectern (176,0)`, `till (192,0)`, `chairF (208,0)`, `chairB (224,0)`, `chairS (240,0)`

**16×16 smalls, y=16** — `stool (160,16)`, `pot (176,16)`, `sack (192,16)`, `bucket (208,16)`, `lamp (224,16)`, `books (240,16)`

**32×16 surfaces, y=32** — `table (0,32)`, `bench (32,32)`, `desk (64,32)`, `workbench (96,32)`, `toolrack (128,32)`, `seedbins (160,32)`, `cart (192,32)`, `altar (224,32)`

**Larger, y=48** — `pew (0,48,48×16)`, `longtable (48,48,48×32)`, `haystack (96,48,32×32)`, `crates (128,48,32×32)`, `wideshelf (160,48,48×16)`, `loom (208,48,32×32)`

`chairF` / `chairB` / `chairS` are front, back and side views — put `chairB` above a table and `chairS` beside it so seating reads correctly.

---

## 6. The six rooms

Reference PNGs are in `reference/`, each with a 16px dark margin standing in for the `Surround`.

### Existing maps — furniture is on real coordinates

**Farmhouse — 14×10** (`room_farmhouse.png`)
Log walls, plank floor. Every position matches `FarmHouseMap.cs`: bed footprint (12,2)–(12,3), chest (2,2), table (6,4)+(7,4), door (7,9), lit windows at x=3 and x=10 on the north wall. Added: stove and a lit hearth on the west/north walls, rug mid-room, bucket and sack in the corners.

**General store — 14×10** (`room_store.png`)
Wainscot walls. Counter runs `counter_l` → `counter_c` ×2 → `counter_r` across row 4 with the till on it, shopkeeper behind. Tall shelves flank the room, `wideshelf` on the north wall, `seedbins` beside the counter.

The four shelf colors correspond to the ratified shelf order — turnip, potato, green bean, cauliflower — so a player can see what's in stock before opening the shop UI. Keep that mapping if the catalog changes.

**Town hall — 40×23** (`room_townhall.png`)
Checkered floor, stone walls — the only interior that should use stone. Long table on a runner up the centre, eight pews in two blocks, clerks' desks at both ends, banners flanking the lectern, plaques on the north wall. Door at (20,22).

At 40×23 the hall is wider than the 30×17 viewport, so the player never sees all of it at once. Worth keeping — it is the only interior with anything to walk toward.

### Proposed maps — sizes are suggestions

**Barn interior — 16×12** (`room_barn.png`)
Dirt floor with hay scatter. `hayloft_edge` along the north wall with a `ladder` up to it, two `stall` dividers, `workbench` + `toolrack`, `cart`, `crates`, `haystack`. Drawn in the derelict state: cobwebs in three corners, floor stains. The restored version is the same layout with the stains removed and the lantern lit.

**Meeting house — 16×14** (`room_church.png`)
Plaster walls, board floor, `rug_a` runner up the centre aisle from the door. Six pews in two blocks, `altar` beneath two `stained` windows, `lectern` to one side, candle stands flanking.

The stained glass is the only place plum and lantern sit adjacent. Worth reserving for exactly this one room — and worth remembering when the town's secrets need somewhere to have been kept.

**Neighbor home — 12×9** (`room_neighbor.png`)
Deliberately smaller than the player's farmhouse. Bed, cradle, hearth, table with two chairs, dresser, loom, one shuttered window beside one lit one.

Built as a template: keep the shell, swap the furniture, and every resident gets a home for the cost of a layout function. The shuttered window is also the cheapest hook for the Act II tell where one house is lit at 01:00 — a different house each week.

---

## 7. Suggested order

1. **Interior atlas** — biggest visible gain for the least work. Three maps already exist; only the tile source changes, and `thehaunt_interior.tres` is drop-in.
2. **Farm soil autotile** — makes farming look intentional. Cell-state function barely changes.
3. **Crops** — decide the 16×32 question first (§3); everything else follows.
4. **Farm exterior + layout** — needs the farmhouse footprint decision.
5. **Barn exterior** — needs a map, a footprint, a Door, and one story flag. The exterior states matter more than the interior, because that is what the player sees from the yard every day.

## Still not drawn

Tool-use animations (till, water, chop — frame table is in the bible's §06), seasonal variants of soil and pasture, animals of any kind, the barn interior's restored state, and the Act II / Act III variants of everything above. All inherit this palette, so they can be added in any order.
