using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Harriet — the afternoon shift at Billie's. Name canon (proposed 2026-08-27;
/// accepted 2026-09-01 / D2).
/// PROPOSED [KEVIN]: sharp ex-schoolteacher, gin; taught half the town. The one
/// patron who says Jane's situation OUT LOUD before the meeting ("Oh, child. You
/// BOUGHT?") — the road strip's bluntest Act-I dread line, still at
/// look-twice-and-move-on strength.
/// Look: cast_billies block 5 (grey bun, glasses — dressed as though the school
/// still expects her).
/// </summary>
public static class Harriet
{
    public const string Id = "harriet";

    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            540, 840,                                        // 3:00 PM - 8:00 PM
            new NpcPlacement(MapIds.BilliesBar, 12, 7, 1)),  // east table, gin
    };

    // The one patron who says it out loud (canon: they let Jane know).
    // [KEVIN] invented copy
    public static readonly DialogueDef Before = Say.Linear("harriet_before", Id,
        "Oh, child. You bought?",
        "...Don't mind me. That's the gin talking. Go on and have your evening.");

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef AfterMeeting = Say.Linear("harriet_after", Id,
        "I taught school in this town for thirty years. Every child in it learned two things off me: long division, and when to stop asking questions.",
        "You'll learn the second one too. The bright ones always do.");

    public static string? Talk(GameData data, GameTime now) =>
        data.HasFlag(StoryKeys.MeetingDone) ? AfterMeeting.Id : Before.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Harriet", CastSheets.Billies, 5, Schedule), Talk, new[] { Before, AfterMeeting });
}
