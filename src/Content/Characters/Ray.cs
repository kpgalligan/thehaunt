using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Ray — the evening shift at Billie's. Name canon (proposed 2026-08-27; accepted
/// 2026-09-01 / D2).
/// PROPOSED [KEVIN]: quiet construction-crew drinker; gets in a little ahead of
/// Nora (the shift seams overlap on purpose).
/// Look: cast_billies block 6 (cropped hair, dust-grey tee — NO hi-vis; he takes
/// the vest off before he comes in, which is what separates him from the crew roles).
/// </summary>
public static class Ray
{
    public const string Id = "ray";

    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            820, GameTime.MinutesPerDay,                     // 7:40 PM - close
            new NpcPlacement(MapIds.BilliesBar, 4, 4, 3)),   // at the bar
    };

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef Default = Say.Linear("ray_default", Id,
        "Long day. We've been patching roofs since the storm — half the town lost shingles.",
        "First one's for my back. Second one's for the quiet.");

    public static string? Talk(GameData data, GameTime now) => Default.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Ray", CastSheets.Billies, 6, Schedule), Talk, new[] { Default });
}
