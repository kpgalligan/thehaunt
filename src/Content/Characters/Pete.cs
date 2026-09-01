using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Pete — the morning shift at Billie's. Name canon (proposed 2026-08-27; accepted
/// 2026-09-01 / D2). Canon base: the bar keeps "a handful of scraggly drunks at all
/// open hours", in shifts that blur at their edges rather than swapping on the hour.
/// PROPOSED [KEVIN]: retired postman; does the crossword aloud, wrong. His shift
/// lingers past Moody's exit — the seam overlap is deliberate, so the room never
/// empties while the bar is open.
/// Look: cast_billies block 2 (bald with grey fringe, wire glasses, pale cardigan).
/// </summary>
public static class Pete
{
    public const string Id = "pete";

    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            240, 560,                                        // 10:00 AM - 3:20 PM
            new NpcPlacement(MapIds.BilliesBar, 2, 7, 2)),   // west table, crossword
    };

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef Default = Say.Linear("pete_default", Id,
        "Morning crowd's the honest crowd. Evening folks drink to forget. We drink to get square with the day.",
        "Seven letters, 'homeward.' Doesn't fit. Never fits.");

    public static string? Talk(GameData data, GameTime now) => Default.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Pete", CastSheets.Billies, 2, Schedule), Talk, new[] { Default });
}
