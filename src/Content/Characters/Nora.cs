using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Nora — the evening shift at Billie's. Name canon (proposed 2026-08-27; accepted
/// 2026-09-01 / D2). Canon base: the evening drunks are joined by "a few more
/// ordinary locals" — Nora is one of those.
/// PROPOSED [KEVIN]: comes for the company, not the drink; the cozy anchor who
/// invites Jane to potlucks and the harvest dance. Normal life, insisting on
/// itself. (The town calendar those lines promise is an open design question —
/// docs/design.md §Open questions.)
/// Look: cast_billies block 7 (loose brown hair, pale green blouse — lightest
/// palette in the bar).
/// </summary>
public static class Nora
{
    public const string Id = "nora";

    // Harriet's seat, inherited by the evening local — the regulars' corner.
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            840, GameTime.MinutesPerDay,                     // 8:00 PM - close
            new NpcPlacement(MapIds.BilliesBar, 12, 7, 1)),
    };

    // The cozy anchor: normal life, insisting on itself. [KEVIN] invented copy
    public static readonly DialogueDef Default = Say.Linear("nora_default", Id,
        "Don't let this room fool you — it's a good town. Potlucks, the harvest dance, the whole calendar.",
        "You should come to things! Being new wears off a lot faster if you let people get a look at you.");

    public static string? Talk(GameData data, GameTime now) => Default.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Nora", CastSheets.Billies, 7, Schedule), Talk, new[] { Default });
}
