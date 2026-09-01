using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Abe — the shack on the east fork (canon name, docs 2026-08-26).
/// CANON (Kevin): his car broke down in town twenty years ago while he was
/// travelling across country; he had a tent, stayed, and built the shack. A bit
/// odd — but he never bought property, so he is not actually a resident,
/// technically speaking. HE CAN LEAVE, which has made him useful to various people
/// in town. We don't learn much about him early on. DEFERRED: his fuller story.
/// PROPOSED [KEVIN] (2026-08-27 commission): gentle, precise, slightly formal;
/// twenty years here and content; never gossips, never elaborates. His one sharp
/// edge is the distinction that rules his life: "I never bought so much as a fence
/// post." After the meeting he offers the thing only he can offer — errands beyond
/// the town line.
/// Look: cast_east block 1 (watch cap, white beard, layered and square — twenty
/// years outside and not one thing about him is ragged; the precision IS the
/// character).
/// </summary>
public static class Abe
{
    public const string Id = "abe";

    // Twenty years at the roadside; where else would he be.
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.EastFork, 26, 20, 0, Ambit: 1)), // beside the shack
    };

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef Before = Say.Linear("abe_before", Id,
        "Car quit on me right about where you're standing. Twenty years back, near enough. Built the shack that autumn.",
        "Town's been kind, mostly. I run errands — fetch things from outside. Folks here find it hard to travel.");

    // The distinction that rules his life (canon: never bought, so never bound).
    // [KEVIN] invented copy
    public static readonly DialogueDef AfterMeeting = Say.Linear("abe_after", Id,
        "So you own now. Hm. I never bought so much as a fence post here. Rented my whole life — best decision I never made on purpose.",
        "You need anything from beyond the town line, you ask me. I make the trip when folks need it. Always have.");

    public static string? Talk(GameData data, GameTime now) =>
        data.HasFlag(StoryKeys.MeetingDone) ? AfterMeeting.Id : Before.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Abe", CastSheets.East, 1, Schedule), Talk, new[] { Before, AfterMeeting });
}
