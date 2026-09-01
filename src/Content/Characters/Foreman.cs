using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The Foreman — leads the town repair crew. Role-labeled by design (Kevin,
/// 2026-09-01 / D3).
/// CANON (Kevin): the crew arrives the morning after the road clears, surprised in a
/// bad way to find a new owner, and tells the player to attend the town-hall meeting
/// that night (the crew beat, IntroBeats.CrewArrival — the foreman fronts it).
/// Look: cast_town block 1 (hi-vis and hard hat — the art contract owns wardrobe).
/// </summary>
public static class Foreman
{
    public const string Id = "foreman";

    // The farm staging is flag-bounded, not clock-bounded, so the crew beat can
    // never be stranded castless.
    public static readonly ScheduleEntry[] Schedule =
    {
        new(StoryKeys.RoadCleared, StoryKeys.CrewArrivalDone,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.Farm, 33, 15, 1)),       // road mouth  [KEVIN]
        new(StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
            IntroRules.MeetingStartMinuteOfDay, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.TownHall, 18, 12, 3)),   // seats  [KEVIN]
        new(StoryKeys.CrewArrivalDone, null,
            120, 600,                                        // 8:00 AM - 4:00 PM
            new NpcPlacement(MapIds.Town, 30, 13, 0, Ambit: 2)),   // roadside  [KEVIN]
    };

    public static readonly DialogueDef Wait = Say.Linear("foreman_wait", Id,
        "Meeting's tonight, at the town hall. Everything gets explained there — hold your questions till then.",   // [KEVIN]
        "We've got our hands full with storm damage until dark anyway.");   // [KEVIN]

    public static readonly DialogueDef After = Say.Linear("foreman_after", Id,
        "So now you know how it is. Takes a while to sit right.",   // [KEVIN]
        "For what it's worth, it's a pleasant town, most days. You'll find your feet.");   // [KEVIN]

    // Silent until the crew beat has run — the beat owns that conversation.
    public static string? Talk(GameData data, GameTime now) =>
        data.HasFlag(StoryKeys.MeetingDone) ? After.Id
        : data.HasFlag(StoryKeys.CrewArrivalDone) ? Wait.Id
        : null;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Foreman", CastSheets.Town, 1, Schedule), Talk, new[] { Wait, After });
}
