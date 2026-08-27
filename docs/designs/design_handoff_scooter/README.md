# Handoff: The Haunt — Electric Scooter

## Overview

A mid-game item. The player finds an electric scooter, rides it at twice walking speed, and can leave it anywhere on any map. It is the only modern manufactured object in a town built in 1958, and it is deliberately the wrong colour for every surface it sits on.

Two things ship here:

1. **Two sprite sheets** — a riding sheet that drop-in replaces the player texture while mounted, and a parked sheet for the scooter as a world object.
2. **A behaviour spec** — mount/dismount, speed, persistence, collision, and interior handling.

Target: Godot 4 / C#, `kpgalligan/thehaunt`, 16px tiles, 480×270 viewport, palette-locked pixel art.

## About the Design Files

`The Haunt - Electric Scooter.dc.html` is a **design document** — it presents the sprites and the spec for review. It is not code to port. Open it in a browser; keep `support.js` beside it.

Unlike previous handoffs in this project, **the sprite sheets here are production assets, not mockups.** `art/scooter_rider.png` and `art/scooter_parked.png` are final, palette-locked, correctly sized, and ready to import. The `*_zoom.png` and `scooter_onroad.png` files are magnified views for the document only — do not ship them.

`art/character.png` is included unchanged, for reference: the riding sheet was composited from its actual pixels, so the two must stay in sync.

## Fidelity

**Production-grade for art. Proposal-grade for game design.**

- The two sheets are final. Every pixel is placed, every colour is from the locked palette plus three new greens.
- The behaviour spec is a proposal. Nothing about a scooter exists in the repo, so node names, flags, and save keys below are named to match existing conventions but are not drawn from code.
- **No battery or charge system is assumed.** That is a real design decision with UI attached and it was not specified. If you want one it needs its own pass.
- **Where the scooter is acquired is unspecified.** "Mid-game" is all the brief said. This matters for art: a found scooter should be scuffed, a purchased one should not.

## Assets

| File | Size | Grid | Ship it? |
| --- | --- | --- | --- |
| `art/scooter_rider.png` | 96 × 96 | 6 cols × 3 rows of 16 × 32 | Yes |
| `art/scooter_parked.png` | 48 × 32 | 3 cols × 1 row of 16 × 32 | Yes |
| `art/character.png` | 96 × 96 | 6 × 3 of 16 × 32 | Already in repo — reference only |
| `art/scooter_rider_zoom.png` | 480 × 480 | — | No, doc only |
| `art/scooter_parked_zoom.png` | 384 × 256 | — | No, doc only |
| `art/scooter_onroad.png` | 576 × 192 | — | No, doc only |

All art was authored by drawing to a canvas at native pixel scale. No photographic or third-party source, nothing traced.

### `scooter_rider.png` — the riding sheet

**The grid is identical to `character.png`**: 96 × 96, six columns by three rows, 16 × 32 cells, rows in the existing order.

| Row | Y range | Facing |
| --- | --- | --- |
| 0 | 0–31 | Toward camera (walking "down") |
| 1 | 32–63 | Profile (walking "side") |
| 2 | 64–95 | Away from camera (walking "up") |

Because the grid matches, this is a **texture swap** — point the existing `AnimatedSprite2D` at this sheet instead of `character.png` and every frame, row order, and flip-for-left-facing behaviour already in place continues to work.

The rider is the unmodified `character.png` art, drawn 6 px higher in the cell so the feet land on the deck. No new character art exists in this sheet, which means the scooter can never drift out of sync with the walk cycle.

**Column 0–5 motion:**

- **Row 1 (profile)** carries a real wheel rotation: a chrome hub spoke advances 60° per column across the six frames. This is the hero read.
- **Rows 0 and 2** carry a 1 px vertical bob on the pattern `[0,0,1,1,1,0]` applied to both rider and scooter together. A 27 px rider in a 16 px cell occludes most of a scooter head-on, so these rows show only what is unambiguous: handlebar, stem, deck, wheel.

#### Geometry, per cell (local coords; `b` = bob offset, 0 or 1)

```
deckY = 25 - b     barY = 16 - b     wheelY = 29 - b     riderLift = 6 - b
```

**Row 1, profile** — draw order matters:

| Order | Element | Rect | Colour |
| --- | --- | --- | --- |
| 1 | Rear wheel | centre (3, wheelY), r = 2 | tire `#171310`, hub `#9a9a8a`, spoke `#b8b5a5` |
| 2 | Front wheel | centre (12, wheelY), r = 2 | same |
| 3 | Deck underside | 3, deckY+2, 10, 1 | `#2d8c46` |
| 4 | Deck | 3, deckY, 10, 2 | `#45bf62` |
| 5 | Deck highlight | 4, deckY, 8, 1 | `#74d98a` |
| 6 | Stem (behind rider) | 11, barY+2, 2, deckY−barY−2 | `#2d8c46` |
| 7 | **Rider** | drawn from `character.png` row 1 at y = −riderLift | — |
| 8 | Stem (in front) | 11, barY+2, 2, 4 | `#2d8c46` |
| 9 | Handlebar | 9, barY, 7, 2 | see handlebar recipe |
| 10 | Forearm | 7, barY+2, 4, 2 | coat `#6b4560` |
| 11 | Forearm shadow | 7, barY+4, 4, 1 | `#2b241d` |
| 12 | Headlamp | 14, barY+3, 1, 2 | housing `#9a9a8a`, lens `#ede3cb` |

**Rows 0 and 2** share one scooter recipe:

| Element | Rect | Colour |
| --- | --- | --- |
| Stem | 8, barY+2, 1, deckY−barY−2 | `#9a9a8a` |
| Deck underside | 3, deckY+2, 10, 1 | `#2d8c46` |
| Deck | 3, deckY, 10, 2 | `#45bf62` |
| Deck highlight | 4, deckY, 8, 1 | `#74d98a` |
| Wheel, edge-on | 7, deckY+3, 2, 4 | `#171310`, hub pixels `#9a9a8a` at y = deckY+4 |
| Ground contact shadow | 6, deckY+7, 4, 1 | `#171310` |
| Handlebar | 2, barY, 12, 2 | see recipe |

Draw order differs by row, and this is the whole reason both rows read correctly:

- **Row 0 (toward camera):** rider → scooter → handlebar. The scooter is in front of the rider.
- **Row 2 (away):** handlebar → rider → scooter. The bar passes behind the body, so only the grips show at the edges; the deck and wheel below carry the read.

**Handlebar recipe** (`x, y, w`):

```
fillRect(x,     y, w,     2)  →  #2b241d   (bar)
fillRect(x + 2, y, w - 4, 1)  →  #b8b5a5   (chrome top face)
fillRect(x,     y, 2,     2)  →  #171310   (left grip)
fillRect(x+w-2, y, 2,     2)  →  #171310   (right grip)
```

**No headlamp in row 0.** Head-on there is nothing to see but the housing, and an earlier version placed it on the rider's coat by mistake. It is omitted deliberately — do not add it back.

### `scooter_parked.png` — the world object

48 × 32, three 16 × 32 cells. The player can leave the scooter anywhere, so they will see this far more often than the riding sprite.

| Cell | X range | View | When to use |
| --- | --- | --- | --- |
| 0 | 0–15 | Side | **Default.** The only view that reads instantly at 1×. |
| 1 | 16–31 | Front | Scooters left facing down a path or against a wall. |
| 2 | 32–47 | Three-quarter | Hand-placed in composed scenes, where a flat side view looks staged. |

Which cell renders should follow the direction the player was facing when they dismounted. Cheap to implement, and it makes the world remember what the player did.

All three stand on a kickstand. The kickstand is joined to the deck underside with a foot — in the front and three-quarter cells it is drawn as a 2 × 3 leg plus a 3 × 1 foot, not a floating blob.

## Interactions & Behavior

| Rule | Spec |
| --- | --- |
| Speed | 2× walking speed while mounted |
| Animation rate | Frame rate scales with speed — same six frames, played 2× faster |
| Mount | Interact with a parked scooter. Player sprite swaps texture. No cutscene, no transition |
| Dismount | Same key. Scooter spawns on the tile the player is standing on |
| Persistence | Map id + tile coords + facing, saved. It stays exactly where it was left, across saves |
| Collision, parked | Blocks walking, is interactable |
| Collision, ridden | Identical to the walking player — no wider hitbox, no new collision shape |
| Interiors | Auto-dismount at the door. It parks itself outside. Never ridden indoors |

### Design intent worth preserving

**A player who leaves the scooter at the far end of the map has to walk back to it.** Losing track of it should be a real, mild inconvenience. Do not add a recall button, a minimap pin, or an auto-return. The scooter having a location the player is responsible for is the only thing that makes leaving it anywhere meaningful.

**Mounting has no ceremony.** No animation, no fade, no camera move. The texture swaps and the speed changes. Anything more turns a convenience into a chore on the twentieth use.

## State Management

| State | Type | Purpose |
| --- | --- | --- |
| `scooter_acquired` | bool | Whether the player has it at all |
| `scooter_map_id` | string | Which map the parked scooter is on |
| `scooter_tile` | Vector2I | Tile coords of the parked scooter |
| `scooter_facing` | enum / int 0–2 | Which parked cell to draw (side / front / three-quarter) |
| `player_mounted` | bool | Drives the texture swap and the speed multiplier |

All local to the save. No data fetching.

When `player_mounted` is true the parked scooter should not exist in the world — there is exactly one scooter, and it is either under the player or on the ground.

## Design Tokens

### New palette slots — three

| Token | Hex | Used for |
| --- | --- | --- |
| scooter-light | `#74d98a` | Deck highlight, stem highlight |
| scooter-base | `#45bf62` | Deck, stem body |
| scooter-dark | `#2d8c46` | Deck underside, stem shadow, frame |

The town's greens are all desaturated and yellow-leaning so they read as vegetation. These are deliberately cooler and far more saturated than anything growing in it — placed on grass (`#4a7c3a`), the scooter still separates cleanly.

### Existing slots reused

| Token | Hex | Used for |
| --- | --- | --- |
| ink-900 | `#171310` | Tires, grips, contact shadow |
| ink-700 | `#2b241d` | Handlebar body, forearm shadow |
| stone-pale | `#b8b5a5` | Chrome top faces, wheel spokes |
| stone-light | `#9a9a8a` | Chrome housings, hubs, stem on front/back views, kickstand |
| cream | `#ede3cb` | Headlamp lens |
| coat purple | `#6b4560` | Forearm, from the existing character palette |

### The headlamp is cold, not amber

Amber `#f2b95c` is reserved for **incandescent interiors** — a rule set when the town's street lights were moved to cold mercury vapour. The scooter is an outdoor object, and a modern scooter has a white LED regardless, so the lamp is a chrome housing with a cream `#ede3cb` lens.

This also keeps the scooter outside the town's warm light entirely, which is the right instinct for the one object here that isn't from 1958.

The lamp appears on the profile row and the parked cells only.

### Grid

| Value | Size |
| --- | --- |
| Tile | 16 × 16 px |
| Character / rider cell | 16 × 32 px |
| Parked scooter cell | 16 × 32 px |
| Viewport | 480 × 270 |

## Files

| File | Contents |
| --- | --- |
| `The Haunt - Electric Scooter.dc.html` | The design document. Open in a browser. |
| `support.js` | Runtime required by the HTML document. Keep it beside the HTML. |
| `art/scooter_rider.png` | **Ship.** Riding sheet, 96 × 96. |
| `art/scooter_parked.png` | **Ship.** Parked sheet, 48 × 32. |
| `art/character.png` | Reference — already in the repo, unchanged. |
| `art/*_zoom.png`, `art/scooter_onroad.png` | Doc illustrations. Do not ship. |

## Open questions for the client

1. **How is it acquired?** Found, bought, or given by a resident all imply different art. A found scooter should probably be scuffed.
2. **Is there a battery?** Nothing here assumes one. A charge meter is a real design decision with UI attached.
3. **Does the town react?** It is the only modern object in a 1958 town. Residents noticing it is free characterisation, if wanted.
4. **Does dread touch it?** Later acts swap tile variants elsewhere. A scooter found somewhere the player did not leave it would cost one sprite and no new art.
