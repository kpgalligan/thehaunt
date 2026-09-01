using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The Mayor — town hall. Role-labeled by design: the intro cast stays unnamed
/// (Kevin, 2026-09-01 / D3).
/// CANON (Kevin): explains the curse to newcomers at the town-hall meeting — the
/// no-leaving wrap, selling, the tribute (the meeting beat, IntroBeats.TownMeeting).
/// Before the meeting the mayor is present-but-silent: the beat owns the conversation.
/// Look: cast_town block 0 (cast-sprites handoff — the art contract owns wardrobe).
/// </summary>
public static class Mayor
{
    public const string Id = "mayor";

    // Town-hall rows start at IntroRules.MeetingStartMinuteOfDay so staging can never
    // drift from the beat window — except the overslept row, which is all-day by
    // design (the relocated wake arrives at dawn, and the hall must never be castless).
    public static readonly ScheduleEntry[] Schedule =
    {
        new(StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
            IntroRules.MeetingStartMinuteOfDay, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.TownHall, 20, 6, 0)),    // podium  [KEVIN]
        new(StoryKeys.Overslept, StoryKeys.MeetingDone,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.TownHall, 20, 6, 0)),    // podium, around the clock
        new(StoryKeys.RoadCleared, null,
            120, 660,                                        // 8:00 AM - 5:00 PM
            new NpcPlacement(MapIds.Town, 24, 19, 0, Ambit: 2)),   // square  [KEVIN]
    };

    public static readonly DialogueDef After = Say.Linear("mayor_after", Id,
        "Settling in? The first days are the hardest — it gets easier, I promise.",   // [KEVIN]
        "Tend your land, meet your neighbors. We look after our own here.");   // [KEVIN]

    // Silent until the meeting has happened — the beat owns that conversation.
    public static string? Talk(GameData data, GameTime now) =>
        data.HasFlag(StoryKeys.MeetingDone) ? After.Id : null;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Mayor", CastSheets.Town, 0, Schedule), Talk, new[] { After });
}
