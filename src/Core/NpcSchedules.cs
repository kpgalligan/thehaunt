namespace TheHaunt.Core;

// Frozen staging table (phase3-spec §4.3) — all staging [KEVIN]. Pure and
// deterministic; NPCs teleport between slots (static staging — no pathing in P3).
// The farm crew staging is flag-bounded, not clock-bounded, so the crew beat can
// never be stranded castless; the mayor's podium row restages the meeting every
// pending evening (missed-meeting recovery is free).
public static class NpcSchedules
{
    // First entry whose flags pass and whose window contains now.MinuteOfDay;
    // null = absent. Window is start-inclusive, end-exclusive.
    public static NpcPlacement? Resolve(NpcDef def, GameData data, GameTime now)
    {
        int minute = now.MinuteOfDay;
        foreach (var entry in def.Schedule)
        {
            if (entry.RequiresFlag != null && !data.HasFlag(entry.RequiresFlag))
            {
                continue;
            }
            if (entry.ForbidsFlag != null && data.HasFlag(entry.ForbidsFlag))
            {
                continue;
            }
            if (minute < entry.StartMinuteOfDay || minute >= entry.EndMinuteOfDay)
            {
                continue;
            }
            return entry.Placement;
        }
        return null;
    }

    // Entry order below is load-bearing (first match wins). Town-hall rows start at
    // IntroRules.MeetingStartMinuteOfDay so staging can never drift from the beat window.

    public static IReadOnlyList<ScheduleEntry> Mayor { get; } = new[]
    {
        new ScheduleEntry(StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
            IntroRules.MeetingStartMinuteOfDay, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.TownHall, 20, 6, 0)),    // podium  [KEVIN]
        new ScheduleEntry(StoryKeys.RoadCleared, null,
            120, 660,                                        // 8:00 AM - 5:00 PM
            new NpcPlacement(MapIds.Town, 24, 19, 0)),       // square  [KEVIN]
    };

    public static IReadOnlyList<ScheduleEntry> Foreman { get; } = new[]
    {
        new ScheduleEntry(StoryKeys.RoadCleared, StoryKeys.CrewArrivalDone,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.Farm, 33, 15, 1)),       // road mouth  [KEVIN]
        new ScheduleEntry(StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
            IntroRules.MeetingStartMinuteOfDay, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.TownHall, 18, 12, 3)),   // seats  [KEVIN]
        new ScheduleEntry(StoryKeys.CrewArrivalDone, null,
            120, 600,                                        // 8:00 AM - 4:00 PM
            new NpcPlacement(MapIds.Town, 30, 13, 0)),       // roadside  [KEVIN]
    };

    public static IReadOnlyList<ScheduleEntry> CrewWorkerA { get; } = new[]
    {
        new ScheduleEntry(StoryKeys.RoadCleared, StoryKeys.CrewArrivalDone,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.Farm, 34, 14, 1)),       // [KEVIN]
        new ScheduleEntry(StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
            IntroRules.MeetingStartMinuteOfDay, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.TownHall, 20, 12, 3)),   // [KEVIN]
        new ScheduleEntry(StoryKeys.CrewArrivalDone, null,
            120, 600,                                        // 8:00 AM - 4:00 PM
            new NpcPlacement(MapIds.Town, 31, 16, 3)),       // [KEVIN]
    };

    public static IReadOnlyList<ScheduleEntry> CrewWorkerB { get; } = new[]
    {
        new ScheduleEntry(StoryKeys.RoadCleared, StoryKeys.CrewArrivalDone,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.Farm, 32, 16, 3)),       // [KEVIN]
        new ScheduleEntry(StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
            IntroRules.MeetingStartMinuteOfDay, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.TownHall, 22, 12, 3)),   // [KEVIN]
        new ScheduleEntry(StoryKeys.CrewArrivalDone, null,
            120, 600,                                        // 8:00 AM - 4:00 PM
            new NpcPlacement(MapIds.Town, 33, 13, 0)),       // [KEVIN]
    };

    // Flag-free and bound to the ShopHours constants so shop-open and
    // shopkeeper-present can never diverge; absent outside hours, never on
    // farm/town/town_hall (intro staging untouched). Silent this phase [KEVIN].
    public static IReadOnlyList<ScheduleEntry> Shopkeeper { get; } = new[]
    {
        new ScheduleEntry(null, null,
            ShopHours.OpenMinute, ShopHours.CloseMinute,     // 9:00 AM - 5:00 PM
            new NpcPlacement(MapIds.GeneralStore, 6, 3, 0)), // behind the counter, facing down
    };
}
