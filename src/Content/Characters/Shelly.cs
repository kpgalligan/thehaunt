using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Shelly — owns the dead drive-in. Name is Kevin's (drive-in doc); her scheduled
/// presence is Kevin's 2026-09-01 / D4 commission ("Shelly should periodically be
/// there, so Jane can learn about it").
/// CANON (Kevin): born in town and stuck here all her life; her parents owned the
/// drive-in and she owns it now. Lives in a room she rents in town (where everybody
/// lives is deliberately unresolved); 63; lives off the savings she stashed away
/// during her years as a nurse in the clinic. She comes back in summer out of
/// nostalgia — opens the chain, sits in a chair, and reads books; there's not much
/// else to do. She holds no hope of the theater reopening, but offers to sell it to
/// Jane — reluctantly, because it is her private space, at a high price ($250k).
/// Selling would free her from the town, but she has no plans to leave: where would
/// she go? The whole restoration arc, her disappearance after the sale and her turn
/// to ALLY, is Story/DriveInArc (PLANNED — no purchase mechanic exists yet, so her
/// offer below is a character's willingness, not a button).
/// PROPOSED [KEVIN] (2026-09-01): the staging, the two defs below, and the voice —
/// even, unhurried, fondness with the shutters half closed.
/// Look: cast_east block 2 (grey bun, pale cardigan over soft blue — appended
/// append-only per the handoff README's 2026-09-01 amendment).
/// </summary>
public static class Shelly
{
    public const string Id = "shelly";

    // Exactly the chain-down window (DriveIn's constants + the season gate), so
    // Shelly-present and chain-open can never diverge: she is the one who opens
    // it. Ambit 0 — she sits by the bench row with a book and does not putter.
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            DriveIn.OpenMinute, DriveIn.CloseMinute,         // 9:00 AM - 6:00 PM
            new NpcPlacement(MapIds.DriveIn, 13, 17, 0),     // the bench row, book open  [KEVIN]
            InSeason: Season.Summer),
    };

    // What the place was — canon restatement: movies, the arcade, the stands for
    // people who came without cars; her parents ran it. [KEVIN] invented copy
    public static readonly DialogueDef A = Say.Linear("shelly_a", Id,
        "Whole town used to fit in this field. Pictures on the screen, the arcade going, the stands full of everybody who didn't drive.",
        "My folks ran it. I grew up on that lot, summers. Now it's me and the book, and honestly the book's company enough.");

    // Ownership and the reluctant offer — the price is canon ($250k), the offer is
    // a willingness, not a mechanic (the arc is planned). [KEVIN] invented copy
    public static readonly DialogueDef B = Say.Linear("shelly_b", Id,
        "Mine now, the whole quiet acre of it. I come out summers to read where the sound used to be.",
        "Would I sell? For a quarter million I'd have to think about it — and I'd think about it a long time. It's the one place in this town that's only mine.");

    public static string? Talk(GameData data, GameTime now) =>
        now.DayIndex % 2 == 1 ? B.Id : A.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Shelly", CastSheets.East, 2, Schedule), Talk, new[] { A, B });
}
