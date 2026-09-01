using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Gloria — the fireworks stand. Name canon (proposed 2026-08-27; accepted
/// 2026-09-01 / D2), and so is her husband Otis's.
/// CANON (Kevin, 2026-08-26): an oddly progressive woman who spent her early years
/// in college fighting to protect abortion rights before Roe v Wade. She met a guy
/// in town while coming to pick up a large amount of cannabis (the town's drug trade
/// — a DEFERRED reveal, winked at and never named), and while doing mushrooms, got
/// married. He died years ago. She sells fireworks to get by. The stand becomes a
/// source of weapons materials later (PLANNED — see Places/FireworksStand).
/// PROPOSED [KEVIN] (2026-08-27 commission): 74, silver braid, radical-grandma
/// warmth; calls everyone "honey" and means it; arrived in '74 "to pick something up
/// for a friend". She is entirely unafraid of the town — which should itself read as
/// strange — and the one adult who talks to Jane like an equal from day one. Voice:
/// long warm sentences with one flint edge.
/// Look: cast_west block 2 (rust cardigan — only she and Bud get the warm rust).
/// </summary>
public static class Gloria
{
    public const string Id = "gloria";

    // Out at the stand in trading hours, same span as the general store's.
    // Ambit 0: Gloria at the stand never moves (Kevin's rule, not an oversight).
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            ShopHours.OpenMinute, ShopHours.CloseMinute,     // 9:00 AM - 5:00 PM
            new NpcPlacement(MapIds.WestEntry, 34, 12, 0)),  // in front of the stand
    };

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef Before = Say.Linear("gloria_before", Id,
        "Well, look at you — the one who bought the old farm. Word gets around, honey. Don't look so alarmed, it's a small town.",
        "There's things about this place that aren't mine to tell you. Go hear the mayor out first. Then come back and we'll talk like grown-ups.");

    // '74, "picking something up for a friend": the drug trade winked at, never
    // named — that reveal is deferred. [KEVIN] invented copy
    public static readonly DialogueDef AfterMeeting = Say.Linear("gloria_after", Id,
        "So they told you. Chin up, honey — I've been furious about it for fifty years and it hasn't spoiled my appetite yet.",
        "I came out here in '74 to pick something up for a friend, and I stayed for a man with a truck full of bad ideas. Otis. Lord, he was fun.",
        "Buy a sparkler before you go. Little lights help. That's not mysticism, that's just true.");

    // The guarded-local swap: franker once the meeting has happened.
    public static string? Talk(GameData data, GameTime now) =>
        data.HasFlag(StoryKeys.MeetingDone) ? AfterMeeting.Id : Before.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Gloria", CastSheets.West, 2, Schedule), Talk, new[] { Before, AfterMeeting });
}
