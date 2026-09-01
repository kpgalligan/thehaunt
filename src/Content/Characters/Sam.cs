using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Sam — owns and operates the hair salon on the east entry (canon name, docs
/// 2026-08-26).
/// CANON (Kevin): gender unclear — THE WRITING USES NO PRONOUNS FOR SAM, EVER
/// (test-enforced, Content_SamIsNeverGendered; the art carries the same rule). Sam
/// only communicates in vaguely poetic statements that MOSTLY rhyme — the lines
/// that don't rhyme are the ones that land. The player can get their hair styled
/// there, but nothing much happens early game in the salon.
/// PLANNED: the haircut itself needs a focused design (Kevin, 2026-09-01 / D6);
/// early game Sam is texture.
/// Look: cast_east block 0 (grey cutting smock — built to refuse the question: no
/// waist, no chest line, no hair length, no jewellery, in any repaint).
/// </summary>
public static class Sam
{
    public const string Id = "sam";

    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            ShopHours.OpenMinute, ShopHours.CloseMinute,     // 9:00 AM - 5:00 PM
            new NpcPlacement(MapIds.Salon, 4, 4, 2, Ambit: 2)), // beside the chair
    };

    // Canon: vaguely poetic, MOSTLY rhymes — the lines that don't are the ones
    // that land. No pronouns for Sam, ever. [KEVIN] invented copy
    public static readonly DialogueDef A = Say.Linear("sam_a", Id,
        "A new head through my door — sit, let me see. Storm-blown and city-cut... we'll set that free.",
        "Scissors know what the season knows: everything grows back. Almost everything grows.");

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef B = Say.Linear("sam_b", Id,
        "Snip, snip — the year turns quick, the light gets thin. Come by before the frost; I'll tuck the summer in.",
        "You wear your worry at the temples. I can cut around it.");

    public static string? Talk(GameData data, GameTime now) =>
        now.DayIndex % 2 == 1 ? B.Id : A.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Sam", CastSheets.East, 0, Schedule), Talk, new[] { A, B });
}
