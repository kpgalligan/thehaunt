namespace TheHaunt.Core;

public enum StoryBeatId { CrewArrival, TownMeeting }

// Pure intro-story rules. Both functions are TOTAL: any flag combination
// (including hostile hand-edited saves) degrades to skip-or-replay, never throws.
// No day-equality term anywhere — beats re-pend until their completion flag lands.
public static class IntroRules
{
    public const int MeetingStartMinuteOfDay = 720;   // 6:00 PM  [KEVIN]

    // [RoadCleared] iff HasFlag(FirstPlanting) && newDayIndex > FlagDay(FirstPlanting)
    // && !HasFlag(RoadCleared); else empty. Evaluated at EVERY dawn — idempotent.
    // No planting ⇒ the road stays blocked forever (no timer). Post-midnight planting
    // stamps the ending day, so the road clears after the upcoming sleep — intended.
    public static IReadOnlyList<string> FlagsToSetOnDayStarted(GameData data, long newDayIndex)
    {
        if (data.HasFlag(StoryKeys.FirstPlanting)
            && newDayIndex > data.FlagDay(StoryKeys.FirstPlanting)
            && !data.HasFlag(StoryKeys.RoadCleared))
        {
            return new[] { StoryKeys.RoadCleared };
        }
        return Array.Empty<string>();
    }

    // CrewArrival is checked first — it wins if both beats pend (hostile save).
    public static StoryBeatId? PendingBeat(GameData data, GameTime now, string activeMapId)
    {
        if (data.HasFlag(StoryKeys.RoadCleared)
            && !data.HasFlag(StoryKeys.CrewArrivalDone)
            && activeMapId == MapIds.Farm)
        {
            return StoryBeatId.CrewArrival;
        }
        if (data.HasFlag(StoryKeys.CrewArrivalDone)
            && !data.HasFlag(StoryKeys.MeetingDone)
            && activeMapId == MapIds.TownHall
            && now.MinuteOfDay >= MeetingStartMinuteOfDay)
        {
            return StoryBeatId.TownMeeting;
        }
        return null;
    }
}
