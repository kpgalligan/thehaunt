using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Repair Worker A — the crew's first hand. Role-labeled by design (Kevin,
/// 2026-09-01 / D3); the crew composition (foreman + two workers) is invented
/// staging [KEVIN].
/// Owns the crew's shared ambient line — <see cref="CrewWorkerB"/> speaks the same
/// def rather than duplicating it.
/// Look: cast_town block 2.
/// </summary>
public static class CrewWorkerA
{
    public const string Id = "crew_worker_a";

    public static readonly ScheduleEntry[] Schedule =
    {
        new(StoryKeys.RoadCleared, StoryKeys.CrewArrivalDone,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.Farm, 34, 14, 1)),       // [KEVIN]
        new(StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
            IntroRules.MeetingStartMinuteOfDay, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.TownHall, 20, 12, 3)),   // [KEVIN]
        new(StoryKeys.CrewArrivalDone, null,
            120, 600,                                        // 8:00 AM - 4:00 PM
            new NpcPlacement(MapIds.Town, 31, 16, 3, Ambit: 2)),   // [KEVIN]
    };

    public static readonly DialogueDef Default = Say.Linear("crew_worker_default", Id,
        "Half a hillside came down on that road. Took us all morning to cut a way through.",   // [KEVIN]
        "Storm left damage all over town, too. We won't be short of work for a while.");   // [KEVIN]

    // Silent through the pending crew beat (the beat owns the conversation).
    public static string? Talk(GameData data, GameTime now) =>
        data.HasFlag(StoryKeys.CrewArrivalDone) || data.HasFlag(StoryKeys.MeetingDone)
            ? Default.Id
            : null;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Repair Worker", CastSheets.Town, 2, Schedule), Talk, new[] { Default });
}
