using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Moody — the morning shift at Billie's. Name canon (proposed 2026-08-27; accepted
/// 2026-09-01 / D2) — a nickname nobody remembers the origin of.
/// PROPOSED [KEVIN]: friendliest man in town before noon, gone by three.
/// Look: cast_billies block 3 (brown mop, green polo — broadest torso of the
/// morning shift, friendliest palette).
/// </summary>
public static class Moody
{
    public const string Id = "moody";

    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            240, 540,                                        // 10:00 AM - 3:00 PM
            new NpcPlacement(MapIds.BilliesBar, 5, 4, 3)),   // at the bar
    };

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef Default = Say.Linear("moody_default", Id,
        "Ha! A new face! Sit down, sit down — I'm everybody's friend till noon, and nobody's after three.",
        "Don't mind the quiet ones in here. Quiet's just how some folks stay comfortable.");

    public static string? Talk(GameData data, GameTime now) => Default.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Moody", CastSheets.Billies, 3, Schedule), Talk, new[] { Default });
}
