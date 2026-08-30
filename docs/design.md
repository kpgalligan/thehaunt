# The Haunt — Design Document

## Canon (user-established — do not alter without Kevin)

- **Setting**: a small New England town. It appears on no map.
- **The hook**: If you own property in town, you cannot leave. If you attempt to do so, you are wrapped around to the other side: drive out past the west entry and you come rolling in from the east, and vice versa (settled in `docs/story/README.md`, implemented as the `RoadWrap` rule). You can sell, if you can find a buyer, but nobody that has left has ever come back. Either because they can't return, or because they die when they leave. Nobody knows for sure. Selling is obviously difficult, because few strangers find the town, and those that visit for trade have heard the stories. Whether they believe them or not, they rarely buy. The town itself is pleasant. The compelled residents try hard to make life as enjoyable as possible. But the evil that stalks the town periodically "wakes up" and demands tribute.
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
  the town hall meeting that night, where the mayor explains the curse. Skipping it by
  going to bed doesn't dodge it (Kevin, 2026-08-28): the player wakes up IN the town
  hall — "(After a much needed nap, you made your way to City Hall. It sounded
  important...)" — and the meeting begins. That's the intro;
  the rest of the story plays out later. *(NOTE, 2026-08-26: `docs/story/README.md` §Main
  Character now gives Jane a partly different backstory — married, a child lost in a fall,
  a divorce — where this bullet says lost touch with friends, parents died, no siblings.
  Both are Kevin-authored; reconcile before writing dialogue that leans on either.)*
- **Combat** (Kevin, answering open question 2): real-time, Stardew-style.
- **The antagonist**: a supernatural, malevolent force in town. It enforces the no-leaving
  rule and manifests various evil entities the player must ultimately contend with.
- **Win condition**: figure out how to defeat the malevolence.
- **Moment-to-moment play**: mundane town-sim activities — farming, mining, fishing, etc. —
  to be extended later with fairly unique specialties (details TBD, user has plans).
- **Progression**: money and connections level up capabilities, weapons, and special items,
  which are required to complete the quests along the path to defeating the malevolence.
- **Skills — planned, NOT designed** (Kevin, 2026-08-29): a skills system is coming;
  establish the plan only — do not design it without Kevin. One of the skills is
  **mechanical repair**. Backstory hook: after Jane's family lost their farm, her father
  worked as an auto mechanic — he'd bring her to work and taught her everything he knew
  about cars. Next to the gas station (west entry) is a closed car repair garage, for
  sale at $100k for now, which Jane will eventually need to buy. Jane needs a more advanced
  mechanical repair skill to get the drive-in running again
  (`docs/story/drivein-movie/README.md`), and in the late game the skills are part of
  her effort to remove the curse on the town.

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
5. Names: the town and the malevolence itself stay unnamed — do not invent. `docs/story/README.md`
   (Kevin, 2026-08-26) is the expanding lore doc and names Jane, Billie, Bud, Sam, Abe;
   `docs/story/cast.md` (2026-08-27, written under Kevin's fill-in-the-blanks commission)
   proposes the rest of the road-strip cast (Walt, Dennis, Gloria, Mr. Pell, the bar
   shifts) — pending Kevin's review. The intro cast (mayor, foreman, crew, shopkeeper)
   remains role-labeled and unnamed. The MOTEL's name is also unwritten by design: the
   motel handoff ships the pole sign with a blank nameplate, and the drive-in's marquee
   carries no name either — both wait on Kevin.

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
  sales report on waking, Tab controls panel. **Phase 3c (started 2026-08-26)**: the road
  strip from `docs/story/README.md` — west_entry, billies (+ the covered pit), fork,
  east_fork (Abe's shack, chained mansion drive), east_entry — as placeholder-art maps,
  the farm rerouted through the fork, and the leave-town wrap (west edge ⇄ east edge)
  implemented and walk-tested. 2026-08-27 (Kevin's fill-in-the-blanks commission): the
  road-strip cast — 14 ambient NPCs with schedules, flag/time-aware dialogue, and four
  interiors (Billie's bar room with the three drunk shifts, the motel lobby, the gas
  station shop, Sam's salon); names/voices proposed in `docs/story/cast.md`, pending
  review. 2026-08-27, from `docs/designs/design_handoff_motel_signage`: the motel is
  now the drawn googie motor court in the west entry (office + four flag-locked guest
  rooms with interiors, asphalt lot, blinking-V pole sign with the blank nameplate —
  the name is deliberately unwritten), the four-mount signage system is applied across
  the road strip (police/hardware/salon bands, Billie's BAR bracket, gas window neon,
  fireworks pole), and the dead drive-in from `docs/story/README.md` is a map off the
  east fork's south side (screen, cracked field, speaker posts, boarded concession,
  "CLO ED" marquee). Jane's drive-in refurbishment goal has no mechanics yet — that
  seam is deliberately empty, like the barn's. Handoff revision (2026-08-27): the town
  is PAVED — asphalt road a value-step darker than the motel lot, kerbs with cuts at
  every driveway, worn centre line — with cobra-head street lights on cold mercury
  vapour replacing the fire lanterns town-wide (the motel's east head is dead by
  design); amber is now strictly interior, and roads turn to dirt past the town line
  (the fork's farm branch stays unsealed). 2026-08-27, from
  `docs/designs/design_handoff_scooter`: the electric scooter — the one modern object
  in town — ships with both production sheets wired in: ridden at 2x walk speed with
  the six-column wheel cycle, parked anywhere with E (three parked views by dismount
  facing), auto-parked at the door of any interior. Kevin's amendments over the
  handoff: the player has it from day one (the mid-game acquisition, and whether it
  arrives scuffed, is an open seam), and it returns to the farmhouse frontage every
  morning no matter where it was left. No battery, no recall, no minimap pin — losing
  track of it until morning is the point. Open handoff questions for Kevin: how it is
  acquired mid-game once the seam closes, whether residents react to it, and whether
  the dread acts touch it. Game-dynamics pass (2026-08-27, per Kevin): the farm map's
  road now leaves SOUTH into the fork's north mouth (the map finally agrees with the
  geography — the blockade, sign, spawn and exit all moved with it); map transitions
  keep the traveller's lane (the crossing offset carries to the destination mouth,
  clamped — a smaller mouth pins to its edge); the motor court parks one guest car per
  occupied room (MotelRules.OccupiedRooms — room 3, Pell's slate sedan, today); and
  NPCs amble around their staging anchors by role (NpcPlacement.Ambit) — proprietors
  putter, seated bar patrons sit, Gloria at the fireworks stand never moves.
  2026-08-28: the overslept summons (skipping the town meeting by going to bed wakes
  the player IN the hall — canon bullet above), and quests + mail — quest defs
  derived purely from story-flag pairs (hand-out/completion), a J quest log,
  completion toasts, the farm mailbox with its raised-flag signal, and the previous
  owner's farewell letter (Kevin's copy verbatim, in LetterDefs) which hands out the
  first quest ("Plant a Few Crops", completed by watering a planted tile). Resolved
  2026-08-28: the letter's promise is now mechanical truth — NewGame stocks the starter
  kit into a chest in the barn (StarterKit -> StorageIds.BarnChest) and the player
  starts empty-handed; fetching the tools is the first errand. Old saves keep their
  inventory-granted kit (frozen migrations) and their barn chest starts empty.
  2026-08-29 (Kevin's garage commission): the closed repair garage beside the gas
  station — a placeholder-art building with a dark GARAGE band and no door, its FOR
  SALE board opening the fourth Menu session (a Buy/Walk-away confirm panel), and
  WorldSim.BuyGarage debiting the 100,000g and stamping `garage.deed` (GarageRules;
  the deed is expected to feed the planned mechanical-repair skill — how is
  undesigned, see Canon §Skills). No interior
  and nothing reads the deed yet — that seam is deliberately empty, like the
  barn's. Still remaining:
  connections/relationship mechanics, enlarging the town centre (clinic, Stumble Inn,
  homes, fountain square), shop catalogs for the gas station and fireworks stand,
  haircuts, motel room unlock stories, the drive-in restoration arc, the garage
  interior + the skills system (plan in Canon; phase and design are Kevin's calls).
- **Phase 4 — Mining & fishing**: plus the unique specialties (design with Kevin
  first). The skills system's plan is recorded in Canon (2026-08-29); which phase it
  lands in, which skill is built first, and the design itself are all Kevin's calls.
- **Phase 5 — The malevolence**: entity manifestations, combat, weapons/special items,
  malevolence quest lines (on the quest framework shipped 2026-08-28), escalation,
  endgame.

Phases 3-5 ordering is flexible; each phase ships playable.
