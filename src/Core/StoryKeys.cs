namespace TheHaunt.Core;

// The only legal story-flag ids in code. A validation test enforces that every
// flag referenced by dialogue defs resolves to a constant here.
public static class StoryKeys
{
    public const string FirstPlanting   = "intro.first_planting";
    public const string RoadCleared     = "intro.road_cleared";
    public const string CrewArrivalDone = "intro.crew_arrival_done";
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
    public const string MotelRoom1Open = "motel.room1_open";
    public const string MotelRoom2Open = "motel.room2_open";
    public const string MotelRoom3Open = "motel.room3_open";
    public const string MotelRoom4Open = "motel.room4_open";
    public const string MotelFull      = "motel.full";
}
