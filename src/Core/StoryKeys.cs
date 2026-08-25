namespace TheHaunt.Core;

// The only legal story-flag ids in code. A validation test enforces that every
// flag referenced by dialogue defs resolves to a constant here.
public static class StoryKeys
{
    public const string FirstPlanting   = "intro.first_planting";
    public const string RoadCleared     = "intro.road_cleared";
    public const string CrewArrivalDone = "intro.crew_arrival_done";
    public const string MeetingDone     = "intro.meeting_done";
}
