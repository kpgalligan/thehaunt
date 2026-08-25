# The Haunt — Design Document

## Canon (user-established — do not alter without Kevin)

- **Setting**: a small New England town. It appears on no map.
- **The hook**: If you own property in town, you cannot leave. If you attempt to do so, you simply arrive back where you tried to leave (or maybe wrap around?). You can sell, if you can find a buyer, but nobody that has left has ever come back. Either because they can't return, or because they die when they leave. Nobody knows for sure. Selling is obviously difficult, because few strangers find the town, and those that visit for trade have heard the stories. Whether they believe them or not, they rarely buy. The town itself is pleasant. The compelled residents try hard to make life as enjoyable as possible. But the evil that stalks the town periodically "wakes up" and demands tribute.
- **Tone of the opening** (Kevin, answering open question 1): the start is entirely cozy,
  except for some NPC conversations. The dread seeps in through dialogue before anything
  else.
- **The protagonist & the opening** (Kevin): the main character grew up on a farm, then
  lived in a big city — went to college, largely lost touch with their friends. Parents
  died; no siblings. Recently laid off, lease up, they took a cross-country road trip to
  think things through. Entering town, they stumbled on the farm: in disrepair, offered at
  a price too good to be true. They bought it on a whim with a handwritten contract and a
  check — unknowingly locking themselves into the curse. The game opens the morning after
  a storm, the road to town blocked: the player learns basic farming and starts repairing
  the facility. The following morning the road is cleared and a repair crew from town
  arrives — surprised, in a bad way, to find a new owner. They tell the player to attend
  the town hall meeting that night, where the mayor explains the curse. That's the intro;
  the rest of the story plays out later.
- **Combat** (Kevin, answering open question 2): real-time, Stardew-style.
- **The antagonist**: a supernatural, malevolent force in town. It enforces the no-leaving
  rule and manifests various evil entities the player must ultimately contend with.
- **Win condition**: figure out how to defeat the malevolence.
- **Moment-to-moment play**: mundane town-sim activities — farming, mining, fishing, etc. —
  to be extended later with fairly unique specialties (details TBD, user has plans).
- **Progression**: money and connections level up capabilities, weapons, and special items,
  which are required to complete the quests along the path to defeating the malevolence.

## Design implications (proposals — treat as draft until confirmed)

- **Structure**: the town sim is the engine, the malevolence is the pressure. The calendar
  system can drive escalation: entities by night, by season, by story act.
- **Dual progression**: money AND connections gate power. Unlike Stardew, where friendship
  is optional flavor, relationships here are mechanically required — the NPC system is core,
  not garnish.
- **Combat is coming** — confirmed real-time, Stardew-style. Nothing built yet;
  architecture should not be surprised by it.
- **The no-leave rule as level design**: map boundaries are diegetic (roads that loop back,
  woods that turn you around) rather than invisible walls. There is no pre-trap window —
  the curse binds at the property purchase, before play begins. The storm-blocked road is
  what gates the tutorial instead.
- **Tribute**: the evil periodically "wakes up" and demands tribute — a natural
  calendar-driven event hook (cadence TBD, see open question 4).
- **Tone axis**: the opening is settled (entirely cozy, dread only via dialogue). How dark
  the mid/late game gets is still open — affects art, writing, and entity design more
  than systems.

## Open questions for Kevin

1. ~~Tone of the opening / backstory?~~ ANSWERED (moved to Canon): entirely cozy start;
   full backstory and intro sequence are canon above.
2. ~~Combat style when it arrives?~~ ANSWERED (moved to Canon): real-time, Stardew-style.
3. The unique specialties extending farm/mine/fish — Kevin's current lean: a supernatural
   wine brew from special grapes. To be discussed in detail before building (Phase 4).
4. Calendar events (festivals? malevolence-driven "bad nights"?). TBD later. The periodic
   tribute demand (now canon) will need a cadence and a cost.
5. Names: town, NPCs, the malevolence itself — all unnamed canon so far. Do not invent. These are all TBD.

## Systems roadmap

- **Phase 1 — Foundation (done)**: clock/calendar, central save model, map contract,
  player movement, interaction, day cycle, HUD, headless test harness.
- **Phase 2 — Core sim loop (done)**: items/inventory/tools, farming vertical slice
  (till/plant/water/grow/harvest), stamina, money, shipping bin. First real save migration.
- **Phase 3 — Town & NPCs (intro slice done)**: maps + transitions (farm/town/town hall),
  NPC schedules on TenMinuteTicked, dialogue system, story flags, and the scripted intro
  (blocked road → first planting → crew arrival → town-hall meeting) shipped 2026-08-25.
  **Phase 3b (shipped 2026-08-25)**: general store (interior map, Shopkeeper 9-5, seed
  buying), farmhouse interior (bed/table/storage chest, save v4), itemized overnight
  sales report on waking, Tab controls panel. Remaining as Phase 3c:
  connections/relationship mechanics, more of the town.
- **Phase 4 — Mining & fishing**: plus the unique specialties (design with Kevin first).
- **Phase 5 — The malevolence**: entity manifestations, combat, weapons/special items,
  quest framework, escalation, endgame.

Phases 3-5 ordering is flexible; each phase ships playable.
