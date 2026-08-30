namespace TheHaunt.Core;

// The only legal story-flag ids in code. A validation test enforces that every
// flag referenced by dialogue defs resolves to a constant here.
public static class StoryKeys
{
    public const string FirstPlanting   = "intro.first_planting";
    // FirstPlanting's sibling, stamped at the bus when a watering lands on a tile
    // that HOLDS A CROP (watering empty tilled soil never stamps it) — it completes
    // the letter's first-crops quest, whose ask ends with "then water them".
    public const string FirstWatering   = "intro.first_watering";
    public const string RoadCleared     = "intro.road_cleared";
    public const string CrewArrivalDone = "intro.crew_arrival_done";
    // Stamped by Main's sleep flow when a bedtime skips the summons
    // (IntroRules.WakesAtTownHall): it keeps the mayor at the podium around the
    // clock (NpcSchedules) and selects the overslept meeting variant
    // (IntroRules.PendingBeat). Monotone like every flag — it outlives the
    // meeting, and MeetingDone gates it dead everywhere it is read.
    public const string Overslept       = "intro.overslept";
    public const string MeetingDone     = "intro.meeting_done";

    // The barn came with the farm and is falling down. Its three drawn states are two
    // monotone flags rather than the handoff's suggested 0/1/2 int, because a flag's
    // value in this model is the DAY it was stamped, not a level (see BarnRules).
    public const string BarnWeathertight = "farm.barn_weathertight";
    public const string BarnRestored     = "farm.barn_restored";

    // The motor court (docs/designs/design_handoff_motel_signage): each guest room
    // unlocks individually — absence = locked, and nothing in Act I sets any of them;
    // the seam is deliberately empty, like the barn's. `motel.full` lights the pole
    // sign's NO circuit, and is expected absent for all of Act I (see MotelRules).
    // Mail: each letter's read stamp (and, for a letter carrying a package, its
    // taken stamp) — LetterDefs references these, and WorldSim's mailbox session
    // (ReadLetter / TakeLetterItems) is the only writer. Reading the farewell
    // letter is also what starts the first-crops quest (QuestDefs).
    public const string FarewellRead = "mail.farewell.read";

    // The west-entry repair garage (docs/story/README.md §West entry): the deed,
    // stamped by WorldSim.BuyGarage when Jane buys the place out of the sale
    // session. Ownership is this one monotone flag, and the operation layer
    // (2026-08-30) hangs off it: the deed-locked shop door, Mike's schedule, the
    // hourly customer roll, and the work presses (GarageOpsRules).
    public const string GarageDeed = "garage.deed";

    public const string MotelRoom1Open = "motel.room1_open";
    public const string MotelRoom2Open = "motel.room2_open";
    public const string MotelRoom3Open = "motel.room3_open";
    public const string MotelRoom4Open = "motel.room4_open";
    public const string MotelFull      = "motel.full";
}
