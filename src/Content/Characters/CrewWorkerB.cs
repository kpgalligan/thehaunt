using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Repair Worker B — the crew's second hand. Role-labeled by design (Kevin,
/// 2026-09-01 / D3). Speaks <see cref="CrewWorkerA"/>'s shared ambient def
/// (registered once, over there) rather than owning a duplicate.
/// Look: cast_town block 3.
/// </summary>
public static class CrewWorkerB
{
    public const string Id = "crew_worker_b";

    public static readonly ScheduleEntry[] Schedule =
    {
        new(StoryKeys.RoadCleared, StoryKeys.CrewArrivalDone,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.Farm, 32, 16, 3)),       // [KEVIN]
        new(StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone,
            IntroRules.MeetingStartMinuteOfDay, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.TownHall, 22, 12, 3)),   // [KEVIN]
        new(StoryKeys.CrewArrivalDone, null,
            120, 600,                                        // 8:00 AM - 4:00 PM
            new NpcPlacement(MapIds.Town, 33, 13, 0, Ambit: 2)),   // [KEVIN]
    };

    // Same window and same shared line as worker A; the def lives with A.
    public static string? Talk(GameData data, GameTime now) =>
        data.HasFlag(StoryKeys.CrewArrivalDone) || data.HasFlag(StoryKeys.MeetingDone)
            ? CrewWorkerA.Default.Id
            : null;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Repair Worker", CastSheets.Town, 3, Schedule), Talk,
        Array.Empty<DialogueDef>());
}
