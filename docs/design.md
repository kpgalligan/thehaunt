# The Haunt — Vision

This file states INTENT: the premise, the long arc, the pillars, and what is still
undecided. It never states what the game does — `src/` does that, and story content
lives in `src/Content` (its CLAUDE.md is the writing contract). A sentence here that
describes current behaviour is a bug: delete it, or make it a test.

## Premise

A small New England town that appears on no map. You can drive into it — it does
exist, it's just not known about, reachable by a side road that is rarely traveled.
The town is nice; the residents work hard to keep it that way, and they have little
capacity to advertise it and significant reason not to. The primary secret: if you
own property in town, you cannot leave — drive out past the west road and you come
rolling in from the east. You can sell, if you can find a buyer; nobody who sold and
left has ever been heard from again — whether they can't return or simply die,
nobody knows. And the haunt that stalks the town is not passive: periodically it
demands a sacrifice — somebody needs to die — and makes the demand known by
activating dangerous entities. Most residents know this only as rumor; few are
directly involved. The town is much more likely to make its demand when strangers
arrive and stay, so the people managing the sacrifices will often abduct a stranger
— better than sacrificing someone from town, although that periodically happens too.

## Seasons — the long arc

Structured like a TV show, in seasons. Season 1: stop the haunt and the sacrifices.
When the curse seems lifted, a deeper problem with the town emerges — Season 2 is
probably preventing the haunt from escaping; Season 3 maybe the military comes to
town and attempts to harness it. Beyond that: the past, or even the future.

## Pillars

- The town sim is the engine; the malevolence is the pressure. The opening is
  entirely cozy — dread arrives only through dialogue.
- Dual progression: money AND connections gate power. Relationships are mechanically
  required, not optional flavor.
- Boundaries are diegetic — roads that wrap, woods that turn you around — never an
  invisible wall.
- Combat, when it arrives, is real-time, Stardew-style (Phase 5).
- Win condition: figure out how to defeat the malevolence.

## Open questions (Kevin's)

1. The unique specialties beyond farm/mine/fish — current lean: a supernatural wine
   brew from special grapes (the farmer's wine at the sale is the planted hook —
   src/Content/Places/Farm.cs). Design together before building (Phase 4).
2. Calendar events — festivals, the potlucks and harvest dance the dialogue
   promises, and the malevolence's tribute cadence and cost.
3. Names, deliberately unwritten until Kevin writes them: the town, the malevolence,
   the motel, the drive-in's marquee, the general store, the intro cast.
4. Where everybody lives (the town centre's homes are planned —
   src/Content/Places/Town.cs).

## Roadmap

- Phases 1–3 — foundation; core sim loop; town, NPCs, the scripted intro, the road
  strip, mail/quests, the garage and skills v1: SHIPPED. Git history and the
  directory CLAUDE.mds are the record.
- Phase 4 — mining & fishing, plus the specialties (design with Kevin first).
- Phase 5 — the malevolence: entities, combat, weapons and special items, the
  malevolence quest lines, escalation, endgame.

Phases ship playable, in flexible order.
