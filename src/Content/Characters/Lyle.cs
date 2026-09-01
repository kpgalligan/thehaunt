using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Lyle — the afternoon shift at Billie's. Name canon (proposed 2026-08-27;
/// accepted 2026-09-01 / D2).
/// PROPOSED [KEVIN]: odd-jobs man; conspiracy-minded about MUNDANE things only —
/// mail times, well water — never the real secret. Turns up a little before three;
/// the shift seams overlap on purpose.
/// Look: cast_billies block 4 (white brimmed cap, tan work shirt — dressed for a
/// job he may or may not have today).
/// </summary>
public static class Lyle
{
    public const string Id = "lyle";

    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            520, 840,                                        // 2:40 PM - 8:00 PM
            new NpcPlacement(MapIds.BilliesBar, 6, 4, 3)),   // at the bar
    };

    // Conspiracy-minded about mundane things ONLY — never the real secret.
    // [KEVIN] invented copy
    public static readonly DialogueDef Default = Say.Linear("lyle_default", Id,
        "You notice the mail comes at different times? Tuesday it was nine. Thursday, noon sharp. I keep a ledger.",
        "And the well water's sweeter on the east side of town. Nobody wants to hear it. I've done tastings.");

    public static string? Talk(GameData data, GameTime now) => Default.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Lyle", CastSheets.Billies, 4, Schedule), Talk, new[] { Default });
}
