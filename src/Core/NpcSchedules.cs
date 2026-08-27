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

    // ------------------------------------------------------------------
    // The road strip (docs/story/cast.md). All ambient, all flag-free, and none of
    // them ever on the farm (the intro-staging invariant). Talkable NPCs stand on
    // OPEN floor beside their counter's open end, never behind it, so the Talk
    // prompt never depends on the probe stretching over furniture.
    // ------------------------------------------------------------------

    // Behind the motel desk from open to close of the day: the lobby IS Walt's life.
    // His conversation, not his placement, tracks the clock (DialogueSelector).
    public static IReadOnlyList<ScheduleEntry> Walt { get; } = new[]
    {
        new ScheduleEntry(null, null,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.Motel, 4, 4, 1)),        // open end of the desk
    };

    // Three weeks into a one-night stay. Mornings in the lobby, evenings in the
    // lobby; the hours between he walks the roads, which never take him anywhere.
    public static IReadOnlyList<ScheduleEntry> Pell { get; } = new[]
    {
        new ScheduleEntry(null, null,
            120, 360,                                        // 8:00 AM - noon
            new NpcPlacement(MapIds.Motel, 8, 2, 0)),        // by the bench
        new ScheduleEntry(null, null,
            780, 1080,                                       // 7:00 PM - midnight
            new NpcPlacement(MapIds.Motel, 8, 2, 0)),
    };

    public static IReadOnlyList<ScheduleEntry> Dennis { get; } = new[]
    {
        new ScheduleEntry(null, null,
            60, 1140,                                        // 7:00 AM - 1:00 AM
            new NpcPlacement(MapIds.GasStation, 7, 4, 2)),   // open end of the counter
    };

    // Out at the stand in trading hours, same span as the general store's.
    public static IReadOnlyList<ScheduleEntry> Gloria { get; } = new[]
    {
        new ScheduleEntry(null, null,
            ShopHours.OpenMinute, ShopHours.CloseMinute,     // 9:00 AM - 5:00 PM
            new NpcPlacement(MapIds.WestEntry, 34, 12, 0)),  // in front of the stand
    };

    // The bar runs 10:00 AM to close. Billie works the room's end of the counter;
    // Bud holds the other end, all open hours, every shift (canon).
    public static IReadOnlyList<ScheduleEntry> Billie { get; } = new[]
    {
        new ScheduleEntry(null, null,
            240, GameTime.MinutesPerDay,                     // 10:00 AM - close
            new NpcPlacement(MapIds.BilliesBar, 2, 4, 2)),   // west end, watching the room
    };

    public static IReadOnlyList<ScheduleEntry> Bud { get; } = new[]
    {
        new ScheduleEntry(null, null,
            240, GameTime.MinutesPerDay,                     // 10:00 AM - close
            new NpcPlacement(MapIds.BilliesBar, 8, 4, 3)),   // the end of the bar
    };

    // The shifts (canon): openers, mid-afternoon replacements, then the evening
    // drunks and the ordinary locals. Canon says "some leave and are replaced", so
    // the seams overlap by a few hands of cards rather than swapping on the hour —
    // and the room never empties while the bar is open.
    public static IReadOnlyList<ScheduleEntry> Pete { get; } = new[]
    {
        new ScheduleEntry(null, null,
            240, 560,                                        // 10:00 AM - 3:20 PM
            new NpcPlacement(MapIds.BilliesBar, 2, 7, 2)),   // west table, crossword
    };

    public static IReadOnlyList<ScheduleEntry> Moody { get; } = new[]
    {
        new ScheduleEntry(null, null,
            240, 540,                                        // 10:00 AM - 3:00 PM
            new NpcPlacement(MapIds.BilliesBar, 5, 4, 3)),   // at the bar
    };

    public static IReadOnlyList<ScheduleEntry> Lyle { get; } = new[]
    {
        new ScheduleEntry(null, null,
            520, 840,                                        // 2:40 PM - 8:00 PM
            new NpcPlacement(MapIds.BilliesBar, 6, 4, 3)),   // at the bar
    };

    public static IReadOnlyList<ScheduleEntry> Harriet { get; } = new[]
    {
        new ScheduleEntry(null, null,
            540, 840,                                        // 3:00 PM - 8:00 PM
            new NpcPlacement(MapIds.BilliesBar, 12, 7, 1)),  // east table, gin
    };

    public static IReadOnlyList<ScheduleEntry> Ray { get; } = new[]
    {
        new ScheduleEntry(null, null,
            820, GameTime.MinutesPerDay,                     // 7:40 PM - close
            new NpcPlacement(MapIds.BilliesBar, 4, 4, 3)),   // at the bar
    };

    // Harriet's seat, inherited by the evening local — the regulars' corner.
    public static IReadOnlyList<ScheduleEntry> Nora { get; } = new[]
    {
        new ScheduleEntry(null, null,
            840, GameTime.MinutesPerDay,                     // 8:00 PM - close
            new NpcPlacement(MapIds.BilliesBar, 12, 7, 1)),
    };

    public static IReadOnlyList<ScheduleEntry> Sam { get; } = new[]
    {
        new ScheduleEntry(null, null,
            ShopHours.OpenMinute, ShopHours.CloseMinute,     // 9:00 AM - 5:00 PM
            new NpcPlacement(MapIds.Salon, 4, 4, 2)),        // beside the chair
    };

    // Twenty years at the roadside; where else would he be.
    public static IReadOnlyList<ScheduleEntry> Abe { get; } = new[]
    {
        new ScheduleEntry(null, null,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.EastFork, 26, 20, 0)),   // beside the shack
    };
}
