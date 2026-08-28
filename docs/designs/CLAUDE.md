# docs/designs/ — the art contract

The five handoff bundles here ARE the art contract — all binding. Each `README.md` is
its integration brief; every `reference/` render and `.dc.html` document is a design
target, not an asset. What differs per bundle is precedence and kind — whether its
`art/` PNGs ship as-is or are re-authored in code — as follows.

## design_handoff_town_art — the base

The base: palette, projection, tile grammar, lighting keys and the act-by-act dread
escalation, with `reference/The Haunt - Art Direction.dc.html` as the full bible.

## design_handoff_farm_interiors — incremental on the base

Incremental on top of town_art: farm soil autotile, crops at 16x32, the barn's three
states, the 64-tile interior atlas and the furniture set (the handoff header says 34
pieces; its own list enumerates 36 and the sheet draws 36 — Furniture.cs documents the
miscount). Where the two conflict, handoff 01 (town_art) wins. The `art/` PNGs of
these first two bundles are the shipped assets, copied into `assets/sprites/` — never
redraw, scale or filter them.

## design_handoff_motel_signage (2026-08-27) — mockups, authored in code

Different in kind: its `art/` PNGs are MOCKUPS, not atlases — the motel facade, pole
sign, and the four sign mounts are authored IN CODE from its pixel-exact spec tables
(MotelFacade, MotelSign, the *Sign nodes, PixelFont, RoadsideTerrain). It also spends
the palette's two reserved neon slots (aqua `#5fb9b0`, red `#e05a3f`) and fixes the
one 3x5 pixel typeface every sign uses.

## design_handoff_scooter (2026-08-27) — production sheets

Ships PRODUCTION sheets again: `scooter_rider.png` (96x96, drop-in grid twin of
character.png) and `scooter_parked.png` (48x32, three views) are shipped assets in
`assets/sprites/`; the `*_zoom`/`_onroad` PNGs are doc-only. Its behaviour spec is
proposal-grade and Kevin amended it: the player has the scooter from the START
(acquisition seam deliberately unbuilt), and it is parked outside the farmhouse every
morning regardless of where it was left (overriding the handoff's "stays where left
forever"). CAUTION: both scooter sheets are authored facing RIGHT and flip for LEFT —
mirrored from character.png's left-facing convention.

## design_handoff_cast_sprites (2026-08-27) — production sheets

Ships PRODUCTION sheets: Jane's new `character.png` (in-place replacement — modern
wardrobe, no more tunic) and the four packed cast atlases copied to
`assets/sprites/cast/` (west/billies/east/town; one 96x96 block per character, block
order fixed by its README). `NpcDef` names a sheet + block; `BodyColor` and the whole
tunic-recolor channel are gone. `art/gen_cast.js` is the wardrobe source of truth —
changing clothes is a spec edit + re-run, never a repaint; the standalone
`cast_<id>.png` files and the stale `cast.png` are unshipped. Dread accents
(plum/bile-green) appear on NO garment in Act I (test-guarded), Sam is never
gendered, Bud's cap carries no insignia. Because the riding sheet is character.png
composited onto the deck, replacing character.png made it stale: `scooter_rider.png`
is now DERIVED art, recomposited by `tools/regen_scooter_rider.py` from the scooter
handoff's recipe tables — rerun it whenever character.png changes. The regenerated
sheet mirrors the profile row so Jane faces the direction of travel (the original
composite left the rider unmirrored — facing backward — under the old art's hat),
and composites her in two parts with a slight knee-bend: at 29px she cannot fit
whole between the cell top and the deck, so her legs lift to land her feet on the
deck while head and torso lift only as far as the cell allows (both measured from
the sheet, so a repaint lands correctly without touching the tool).
