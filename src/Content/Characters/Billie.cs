using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Billie — owns the dive bar that bears his name (canon name, docs 2026-08-26).
/// CANON (Kevin): walks with a limp, a patch over one eye. Early game Jane is barely
/// welcome, and both the patrons and the proprietor let her know it. Later he will
/// be an ALLY: he's fought the evils in town and would prefer to break the curse
/// than live with it — but he's a beaten man. DEFERRED: that curse-fighting past
/// stays entirely unspoken in Act I.
/// PROPOSED [KEVIN] (2026-08-27 commission): the hostility has one inch of decency
/// in it — the stool by the door is nobody's. After the meeting: "your money's the
/// same color as anybody's." Voice: economy; serves without looking.
/// Look: cast_billies block 0 (the patch is a 2x2 ink block — deliberately small).
/// </summary>
public static class Billie
{
    public const string Id = "billie";

    // The bar runs 10:00 AM to close. Billie works the room's end of the counter.
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            240, GameTime.MinutesPerDay,                     // 10:00 AM - close
            new NpcPlacement(MapIds.BilliesBar, 2, 4, 2, Ambit: 3)), // west end, working the room
    };

    // Barely welcome, canon — with one inch of decency in it. [KEVIN] invented copy
    public static readonly DialogueDef Before = Say.Linear("billie_before", Id,
        "We're pouring for regulars tonight.",
        "...Stool by the door's nobody's. If you're going to sit, sit quiet.");

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef AfterMeeting = Say.Linear("billie_after", Id,
        "You went to the meeting, so you know why nobody in here is looking for a new friend.",
        "A drink's a drink, though. Your money's the same color as anybody's.");

    public static string? Talk(GameData data, GameTime now) =>
        data.HasFlag(StoryKeys.MeetingDone) ? AfterMeeting.Id : Before.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Billie", CastSheets.Billies, 0, Schedule), Talk, new[] { Before, AfterMeeting });
}
