# Handoff: Cast Sprites

## Overview

Twenty character sprite sheets for The Haunt — Jane (the player) plus the fourteen road-strip cast members in `docs/story/cast.md` and the five NPC role ids already in `src/Core/NpcDefs.cs`. They replace the procedural placeholder in `src/World/PlaceholderSprites.cs`.

The brief that produced them: the town's *buildings* are stuck in the late 1950s because nothing new has been built there, but the *people* live in the present. The previous placeholder sprites read as period farmhands. These do not — every character wears plain, current, off-a-rack clothing: tees, jeans, ball caps, a zip hoodie, cardigans, a hi-vis vest, work aprons. Plain, not sophisticated. No waistcoats, no long skirts over aprons, no bonnets.

Jane got the most attention: she is a laid-off city woman who grew up on a farm, and she is dressed as someone who packed a trailer and drove, not as someone who bought a costume for the countryside.

## About the design files

The PNGs in `art/` are **finished production assets, not references** — palette-locked, correct dimensions, correct cell grid. Drop them in.

The two other files are references:

- `art/gen_cast.js` — the generator that produced every sheet. Plain JavaScript, canvas-based, no dependencies. It is the *source of truth for wardrobe*: each character is a small spec object (hair style, shirt/over/pants/shoes colours, flags like `slim`, `stoop`, `beard`, `glasses`, `patch`, `cap`, `hood`, `skirt`, `apron`, `smock`, `vest`, `tie`). Changing a character's clothes is a spec edit and a re-run, not a repaint. Keep it in the repo next to the art.
- `reference/The Haunt - Cast Sprites.dc.html` — the design document: animated previews of every sprite and the reasoning behind each wardrobe. A design reference, not code to ship.

## Fidelity

**High fidelity for everything mechanical; deliberate placeholder quality for the artwork itself.**

The palette, dimensions, cell grid, animation timings, atlas layouts and per-character wardrobe are final — treat every number here as exact. The pixel craft is honest placeholder work: readable, consistent, palette-correct, and good enough to ship a vertical slice on. When a pixel artist replaces it, they should keep the specs in this document and the silhouette rules below, and improve the rendering.

Do not "improve" the art in code. If a sprite looks wrong in the engine it is almost certainly an import setting.

## Assets

All at native 16px. Cells are **16 wide × 32 tall**.

| File | Size | Contents |
| --- | --- | --- |
| `art/character.png` | 96×96 | Jane. **Replaces the existing file in place.** |
| `art/cast_west.png` | 480×96 | walt, dennis, gloria, pell, mike |
| `art/cast_billies.png` | 768×96 | billie, bud, pete, moody, lyle, harriet, ray, nora |
| `art/cast_east.png` | 192×96 | sam, abe |
| `art/cast_town.png` | 480×96 | mayor, foreman, crew_worker_a, crew_worker_b, shopkeeper |
| `art/cast_<id>.png` | 96×96 | Each of the 19 NPCs standalone, for per-NPC loading |

In every packed atlas, character *n* occupies `x = n * 96 .. n * 96 + 95`, full height. Use either the packed atlas or the standalone files — they are pixel-identical.

> **Amendment (2026-08-30, Kevin's garage-operation commission):** `mike` (garage
> clerk; the name is Kevin's) appended to `cast_west` as block 4 — append-only, the
> original four blocks byte-preserved. His spec lives in `art/gen_cast.js` like
> everyone's; the atlas was regenerated with `tools/run_gen_cast.mjs` (a local
> harness for this file's generator) and recomposed so blocks 0-3 kept their
> shipped bytes. No standalone `cast_mike.png` was produced (the standalones are
> unshipped).

### Sheet layout — 96×96, 16×32 cells

| | col 0 | col 1 | col 2 | col 3 | col 4 | col 5 |
| --- | --- | --- | --- | --- | --- | --- |
| **row 0** (y=0) — facing 0, down | idle A | idle B | walk 1 | walk 2 | walk 3 | walk 4 |
| **row 1** (y=32) — facing 1, left | idle A | idle B | walk 1 | walk 2 | walk 3 | walk 4 |
| **row 2** (y=64) — facing 3, up | idle A | idle B | walk 1 | walk 2 | walk 3 | walk 4 |

- Facing encoding is unchanged: `0=down, 1=left, 2=right, 3=up`.
- **Facing 2 (right) is a horizontal flip of row 1.** There is no fourth row.
- Feet sit on the bottom row of the cell. The sprite occupies one tile of floor and overhangs one tile upward.
- Everything is fully outlined in `ink-700 #2b241d`. Transparent elsewhere.

### Animation

- **Idle** — cols 0–1, 2 frames at **1.5fps**. Frame B lowers the head 1px (breath). Nothing else moves.
- **Walk** — cols 2–5, 4 frames at **8fps**. Cycle is contact / pass / contact / pass. Cols 3 and 5 are the same pass frame with the body raised 1px.
- **The bob is baked into the frames. Do not add engine-side bobbing or easing.** Play both at a flat rate.

### Import settings (Godot)

Filter **off**, mipmaps **off**, compression **none** (lossless). Same as the existing town atlases. A sprite that looks blurry or fringed is an import setting, not the art.

## Characters

Jane and each NPC below. "Silhouette" is the head shape that identifies the character at 1× — it is the load-bearing part of each design and must survive any repaint.

### Jane — player · `art/character.png`
- **Silhouette:** low auburn ponytail, drawn on all three facings (runs down the jacket from behind, clears the collar in profile).
- **Wardrobe:** cream tee, open olive chore shirt, straight jeans, dark boots.
- **Build:** narrow — shoulders `x5..x10`, body `x4..x11`, arms at `x3`/`x12`. One pixel narrower each side than the default male build, so she reads slighter than the crowd at Billie's.
- **Colours:** hair `#5a4a3a` base / `#7a4a34` light · tee `#ede3cb` / `#ffffff` / `#b8b5a5` · chore shirt `#457539` / `#5f9445` / `#2f5228` · jeans `#47788c` / `#5f8fa3` / `#2e5566` · boots `#4a3526`.
- **Rationale:** cream at the chest is her value anchor against dark interiors; olive keeps her out of a fight with grass; jeans are her only saturated block. Nothing she wears was bought for farming.

### West entry — `art/cast_west.png`

| id | Silhouette | Wardrobe | Notes |
| --- | --- | --- | --- |
| `walt` | grey, thinning | brown windbreaker over pale shirt, dark slacks | Weathered skin (`#c49a72`/`#a87b56`) and a 1px stoop carry the drinking. Nothing else signals it. |
| `dennis` | dark mop, wider than his head | grey zip hoodie over dark tee, dark jeans | Slim build. The only sprite deliberately wearing this decade. |
| `gloria` | silver braid down the back | rust cardigan over cream, denim skirt, boots | Braid is her read from behind. Rust `#a4432f` is the warmest colour on the road and only she and Bud get it. |
| `pell` | neat side part | charcoal sport coat, white shirt, dark tie | Slim-cut and immaculate. Three weeks into a one-night stay and not one pixel is rumpled — that is his only tell. No dread accent on him. |
| `mike` | brimless cap / watch cap | warm tan shirt, dark trousers | 2026-08-30 amendment (garage clerk; block 4). The counter's clothes, not the pit's — no smock, no coveralls: he is not a mechanic and the wardrobe says so. Cap in `waterMid` — the only blue head on the strip, so he never reads as walt/dennis/pell. |

### Billie's — `art/cast_billies.png`

| id | Silhouette | Wardrobe | Notes |
| --- | --- | --- | --- |
| `billie` | short dark hair + eye patch | dark tee, grey bar apron, black trousers | Patch is a 2×2 `ink-900` block over his left eye with a 1px strap. Deliberately small — a full brow band destroys the face at 1×. 1px stoop. |
| `bud` | dark green brimmed cap + white beard | rust flannel over pale shirt, jeans | **The cap carries no insignia and never will** — the war is unnamed in the art too. 1px stoop. |
| `pete` | bald with grey fringe + wire glasses | pale cardigan over blue shirt, khakis | Slim, 1px stoop. Retired-postman tidy. |
| `moody` | brown mop | green polo, brown work trousers | Broadest torso of the morning shift, friendliest palette. |
| `lyle` | white brimmed cap | tan work shirt, jeans | Dressed for a job he may or may not have today. |
| `harriet` | grey bun + glasses | navy blouse under grey cardigan, dark skirt | Slim. Dressed as though the school still expects her. |
| `ray` | cropped hair | dust-grey tee, tan work trousers, boots | **No hi-vis** — he takes the vest off before he comes in. Distinguishes him from the crew roles. Weathered skin tone. |
| `nora` | loose brown hair past the shoulders | pale green blouse, jeans, light sneakers | Slim. Lightest palette in the bar — she came for the company, not the drink. |

Order in the atlas is the shift order: `billie, bud, pete, moody, lyle, harriet, ray, nora`. No two adjacent drinkers share a head shape or a torso colour, because the shifts overlap on screen.

### East entry and the fork — `art/cast_east.png`

| id | Silhouette | Wardrobe | Notes |
| --- | --- | --- | --- |
| `sam` | cropped black hair, no other head feature | grey cutting smock to the hip, dark trousers | **Built to refuse the question.** Narrow frame, no waist, no chest line, no hair length, no jewellery. The smock is the entire silhouette. Do not gender this sprite in a repaint. |
| `abe` | brown watch cap + white beard | dark green coat over tan shirt, brown trousers | Slim, layered, and square — twenty years outside and not one thing about him is ragged. That precision is the character. |

### Town centre — `art/cast_town.png`

The five ids in `src/Core/NpcDefs.cs`, in `All` order. These are phase-3 beat roles, not road-strip cast: they have no names in `cast.md` and none here.

| id | Silhouette | Wardrobe |
| --- | --- | --- |
| `mayor` | grey thinning hair | navy polo, khakis |
| `foreman` | white hard hat | hi-vis vest over grey shirt, dark jeans |
| `crew_worker_a` | navy brimmed cap | blue tee, grey work pants |
| `crew_worker_b` | brown bun | hi-vis vest over green tee, jeans; weathered skin tone |
| `shopkeeper` | grey thinning hair | button-down, canvas work apron, dark trousers |

Two deliberate omissions, per canon: the police station and hardware store are cast-empty, and the drive-in has no cast. No sprites exist for them.

## Implementation

### 1. Replace `PlaceholderSprites`

`src/World/PlaceholderSprites.cs` currently builds a 16×22 `ImageTexture` procedurally at `Character(int facing, Color tunic)`, tinting a tunic block to tell characters apart. That whole approach goes away:

- Cell height changes **16×22 → 16×32**. Anything that assumes 22 (offsets, y-sorting, camera framing, collision anchors) needs a pass.
- `NpcDef.BodyColor` becomes dead for rendering. Replace it with a sprite reference — an atlas path plus a block index, or just a path to the standalone `cast_<id>.png`. The `#8a4a7a`-style hexes in `NpcDefs.cs` and the `/* [KEVIN] */` marker on the shopkeeper can go.
- Load with `AtlasTexture` regions over a single `Texture2D` per atlas, or `AnimatedSprite2D` with a `SpriteFrames` built from the 16×32 grid. Right-facing is `flip_h = true` on the left row — do not author a fourth row.

### 2. Suggested minimal shape

```
NpcDef(Id, DisplayRole, SpriteSheet, SpriteBlock, Schedule)
   SpriteSheet  "res://art/cast_billies.png"
   SpriteBlock  0            // x offset = block * 96
```

Jane loads `res://art/character.png` with block 0.

### 3. Animation state

Per character sprite: `facing` (0–3) and `moving` (bool).

- `moving == false` → play idle: cols 0–1 at 1.5fps.
- `moving == true` → play walk: cols 2–5 at 8fps.
- Row = `facing == 3 ? 2 : facing == 0 ? 0 : 1`; `flip_h = facing == 2`.

No other state. No blending, no transitions, no engine-side bob.

## Design tokens

Every pixel in these sheets comes from the project palette in the Art Direction Bible. Two slots remain reserved.

**Ink** `ink-900 #171310` · `ink-700 #2b241d` · `ink-500 #453a2e` · `cream #ede3cb` · `stone-pale #b8b5a5`
**Green** `green-dark #2f5228` · `green-mid #457539` · `green-base #4a7c3a` · `green-light #5f9445` · `green-pale #86ad5c`
**Earth** `earth-dark #4a3526` · `wood-warm #6b4a2f` · `earth-mid #7a5b3c` · `earth-base #8a6a45` · `earth-light #a5855c`
**Stone** `stone-dark #3e4241` · `stone-shade #575a58` · `stone-base #7a7a7a` · `stone-light #9a9a8a` · `barn-red #a4432f`
**Sky/water/flesh** `sky-day #8fb8cf` · `water-mid #47788c` · `water-deep #2e5566` · `skin-base #e8c8a0` · `skin-shade #c49a72`
**Accents** `lantern #f2b95c` · `hair-stock #5a4a3a` · `plum #6b4560` · `bile-green #7d8f4a` · `bone #cfd6d1`

Four light steps outside the named palette are used only as the fifth ramp step on garments: `#ffffff` (cream light), `#5f8fa3` (denim light), `#c25c44` / `#732c1f` (barn-red light/shade), `#bfa07a` (earth-light light), `#a2c477` (green-pale light), `#a87b56` (skin-shade shade), `#ffd98a` (lantern light), `#7a4a34` (auburn light). No sixth step anywhere.

### Wardrobe mapping — no new palette slots were spent

| Garment | Palette |
| --- | --- |
| Denim (jeans, skirt) | `water-mid` / `water-deep` |
| Hi-vis vest | `lantern` |
| Flannel, cardigan (Gloria, Bud) | `barn-red` |
| Work aprons | `earth-mid` (shop) / `stone-shade` (bar) |
| Auburn hair | `hair-stock` + `#7a4a34` |
| Grey/white hair | `stone-light` + `bone` |
| Hoodie, smock, suit | `stone-dark` / `stone-shade` / `stone-base` |

## Rules any future character art must follow

1. **One head shape per character.** At 16px wide the head is the only reliable identifier. Shapes in use: ponytail, braid, bun, long, mop, cropped, short, thin, bald, brimmed cap, brimless cap/watch cap, hard hat, hood. No two characters who appear in the same frame may share one.
2. **Two garment colours per torso, plus a shade step.** A shirt and one layer — jacket, cardigan, vest, apron, smock. A third colour becomes noise the moment the sprite moves.
3. **No new palette slots.** Two remain reserved. Dither between two existing steps rather than adding a sixth.
4. **Light from the upper left, always.** Highlights on the top and left edges; shade on the lower right.
5. **Dread accents stay off people.** `plum`, `bile-green` and `bone` appear on no garment in Act I — Pell included. The first character to wear one is the reveal, and spending them early spends the whole effect.
6. **Sam is never gendered.** No waist, no chest line, no hair length, no jewellery, in any repaint.
7. **Bud's cap carries no insignia.** The war stays unnamed in the art as it does in the dialogue.

## Files in this bundle

```
art/character.png              Jane, 96×96
art/cast_west.png              480×96   (2026-08-30: +mike, block 4)
art/cast_billies.png           768×96
art/cast_east.png              192×96
art/cast_town.png              480×96
art/cast_<id>.png              19 standalone 96×96 sheets
art/gen_cast.js                the generator — keep this in the repo
reference/…Cast Sprites.dc.html  the design document (reference only)
```

## Source of the design

- `docs/story/cast.md` — the cast, their roles and their voices
- `docs/story/README.md` — Jane's history and the town's geography
- `src/Core/NpcDefs.cs` — the five role ids that exist in code
- `src/World/PlaceholderSprites.cs` — what these replace
- The Art Direction Bible (`design_handoff_town_art/README.md`) — palette, projection, outline and lighting rules
