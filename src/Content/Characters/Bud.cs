using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Bud — the end of the bar, open to close, every shift (canon name, docs 2026-08-26).
/// CANON (Kevin): old, and has seen some shit. He moved to town after Vietnam — a
/// fact the game will probably reveal later (Kevin, 2026-09-01 / D10), so the WAR
/// STAYS UNNAMED in dialogue and his cap carries no insignia (art rule). There is
/// going to be a lot to reveal about "Bud" later — Kevin's own quotation marks;
/// deeper story deferred, do not foreclose anything about him.
/// PROPOSED [KEVIN] (2026-08-27 commission): oblique; occasionally drops the truest
/// sentence in the room and goes back to his glass.
/// Look: cast_billies block 1 (green brimmed cap, white beard, rust flannel).
/// </summary>
public static class Bud
{
    public const string Id = "bud";

    // Bud holds the far end of the bar, all open hours, every shift (canon).
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            240, GameTime.MinutesPerDay,                     // 10:00 AM - close
            new NpcPlacement(MapIds.BilliesBar, 8, 4, 3)),   // the end of the bar
    };

    // The war and the deeper story stay unnamed (deferred). [KEVIN] invented copy
    public static readonly DialogueDef A = Say.Linear("bud_a", Id,
        "Been holding this stool since before the paint on that door.",
        "You'll settle, or you won't. Either way the town gets its way. Drink up.");

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef B = Say.Linear("bud_b", Id,
        "Some places you leave. Some places you're from. I quit sorting out which is which a long way back.",
        "It's a good bar. That part's simple.");

    public static string? Talk(GameData data, GameTime now) =>
        now.DayIndex % 2 == 1 ? B.Id : A.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Bud", CastSheets.Billies, 1, Schedule), Talk, new[] { A, B });
}
