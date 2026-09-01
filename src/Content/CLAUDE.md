# src/Content — the story layer (characters, places, arcs, words)

The single source of truth for story content. If the game does it, code here (or in
Core) says it — no markdown describes runtime behaviour, content, numbers, hours,
prices or state, anywhere in the repo. `docs/design.md` is vision and open questions
only. This layout shipped 2026-09-01 from `docs/content-spec.md` (deleted after
execution; the artifact copy holds the plan and Kevin's twelve decisions).

## Layering (test-enforced)

- PURE C# — no `using Godot` (Source_ContentHasNoGodotDependency), so content stays
  testable without a scene tree, like Core.
- Direction: `Systems / World / UI / Story -> Content -> Core`, never back
  (Source_CoreNeverReferencesContent). Core keeps the types (NpcDef, DialogueDef,
  QuestDef, LetterDef, DialogueSession), the schema registries (StoryKeys, MapIds,
  SkillIds, StorageIds), and every engine system — the whole garage stays in Core
  because GarageOpsRules reads GarageRules.

## Organisation: one file per subject

- `Characters/` — one file per cast member: CANON/PROPOSED header, the schedule
  table, the dialogue defs, the `Talk` selector, and the `CharacterDef` — everything
  the character IS, on one screen. `Characters.All` is the canonical order; the
  mechanism registries (`NpcDefs`, `DialogueDefs`, `DialogueSelector`) are pure
  derivations over it, and their public APIs never change. `Jane.cs` is the player
  (backstory only — never staged). A character file must never reference
  `Characters` (registry cycle).
- `Dialogue/` — the derived registry + selector, and `Say` (Linear/Graph builders).
  Multi-speaker beat dialogues belong to their ARC, not a character
  (`Story/IntroBeats`).
- `Places/` — a file per location that has rules, hours, copy, or design worth
  recording: the model side of a place. The three homes of a place never blur: the
  RECIPE (data/maps) says where things stand, the MAP CLASS (src/World) says how
  they are drawn, the PLACES file says what the place means and its rules.
- `Story/` — arcs: IntroRules + IntroBeats (the scripted opening), QuestDefs/
  QuestRules, LetterDefs/MailRules, and planned arc stubs (DriveInArc).

## Rules (violations are bugs)

- **Ids are forever; names are free.** `walt`, `walt_sharp`, `intro.meeting_done`
  never change (schedules, saves, tests and the director key on them); display
  names and copy change in one place. Story-flag ids stay constants in Core's
  StoryKeys (validation-tested).
- **State-dependence is a pure read of `(GameData, GameTime)`** — selectors,
  schedules and derivations, never a prose qualifier ("early game", "later").
- **Static init order.** C# initialises static fields in TEXTUAL order: declare
  `Schedule` and the dialogue defs BEFORE the `Def`/`All` that lists them, or the
  registry captures nulls (IntroBeats keeps `All` at the bottom for this reason).
- **CANON / PROPOSED / PLANNED / DEFERRED** blocks head every subject file:
  - CANON — Kevin's facts and decisions, dated. Never invent beyond it without
    asking; write a proposal INTO the file, marked.
  - PROPOSED `[KEVIN]` — Claude-invented, awaiting Kevin. `[KEVIN]` is the review
    queue: `rg "\[KEVIN\]" src`.
  - PLANNED — designed but unbuilt, living where it will be built, registered
    NOWHERE (test-guarded: Content_PlannedStubsAreUnregistered). The planned
    queue: `rg "PLANNED" src/Content`.
  - DEFERRED — reveals the writing must walk up to and stop short of.
- **Appearance is the art contract's** (gen_cast.js + the cast-sprites README,
  append-only blocks): a character file points at its sheet + block and never
  restates wardrobe.
- **Content tests are the sync mechanism** (src/Tests/ContentTests.cs +
  Dialogue_DefsValidate + the staging tests). A canon writing rule that can be
  checked is checked.

## Writing rules (Act I — binding)

- Act I is ENTIRELY cozy; dread arrives only through dialogue, only at
  look-twice-and-move-on strength.
- The town's open secret shapes every conversation: before `meeting_done` every
  local knows something Jane doesn't, so their kindness has pity in it; after it,
  the masks may slip half an inch.
- DEFERRED reveals — write up to them and stop: the pit (NOBODY mentions it —
  test-enforced), the drug trade (Gloria's '74 wink is the ceiling), who manages
  the sacrifices, Bud's war (canon: Vietnam — unnamed in dialogue, no insignia on
  the cap; Kevin: much more to reveal about "Bud" later), Abe's full story,
  Billie's curse-fighting past, the farmer's wine (Places/Farm), and Pell is never
  threatened on screen.
- Sam takes no pronouns, ever — in dialogue, letters, and any line about Sam
  (test-enforced).
- Deliberately unnamed, waiting on Kevin: the town, the malevolence, the motel
  (blank nameplate), the drive-in's marquee, the general store, the intro cast
  (mayor / foreman / repair workers / shopkeeper — role labels, D3).
- Dialogue is authored short: a linear def is 2-3 lines; letters fit the mailbox
  panel unscrolled (~15 wrapped lines at font 8).
