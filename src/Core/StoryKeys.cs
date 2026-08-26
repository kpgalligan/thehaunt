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
}
