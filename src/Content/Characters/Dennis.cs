using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Dennis — the gas station clerk. Name canon (proposed 2026-08-27; accepted
/// 2026-09-01 / D2).
/// CANON (Kevin, 2026-08-26): a 20-something incel; sarcastic conversation, rarely
/// insightful. The station sells food now, and mission-useful pills with side
/// effects later (PLANNED — see Places/GasStation).
/// PROPOSED [KEVIN] (2026-08-27 commission): pathetic-funny, never menacing. The
/// only person in town who finds the town weird in MUNDANE ways — the hand-crank
/// pumps, the missing signal, "nobody else seems bothered" — which makes him
/// accidentally the player's surrogate. He will complain forever and never leave.
/// Voice: deadpan retail despair.
/// Look: cast_west block 1 (grey zip hoodie — the only sprite deliberately wearing
/// this decade).
/// </summary>
public static class Dennis
{
    public const string Id = "dennis";

    // The staffed window is GasStation's constants, shared with the west entry's
    // OPEN neon so the sign can never lie about whether Dennis is at the counter.
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            GasStation.OpenMinute, GasStation.CloseMinute,   // 7:00 AM - 1:00 AM
            new NpcPlacement(MapIds.GasStation, 7, 4, 2, Ambit: 2)), // open end of the counter
    };

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef A = Say.Linear("dennis_a", Id,
        "Welcome to the gas station. Pumps are hand-crank. The owner says electric's 'more trouble than it's worth out here.' The man runs a gas station.",
        "Jerky's on the shelf. It's from... a while ago. It's aged. Like wine, if wine were jerky.");

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef B = Say.Linear("dennis_b", Id,
        "No, there's no signal. No, there's no tower. Yes, I've asked. Nobody else seems bothered, which is — fine. Totally normal town.",
        "We've got pills for headaches. We've got pills for... other stuff. My advice? Don't read the labels too hard.");

    // Alternates by day parity so two visits in a row are never identical.
    public static string? Talk(GameData data, GameTime now) =>
        now.DayIndex % 2 == 1 ? B.Id : A.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Dennis", CastSheets.West, 1, Schedule), Talk, new[] { A, B });
}
