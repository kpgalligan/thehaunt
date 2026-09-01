using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Mr. Pell — the motel's one guest, room 3. WHOLLY INVENTED character (proposed
/// 2026-08-27; name canon per Kevin's 2026-09-01 / D2), grounded in canon's "the
/// motel is where many of the sacrifices will ultimately come from" and "the town is
/// much more likely to make the demand when strangers arrive and stay".
/// PROPOSED [KEVIN]: a notions salesman three weeks into a one-night stay,
/// unnervingly content — sleeps deeper than he has since boyhood, keeps meaning to
/// leave, finds it "a kind of mercy" that leaving stops seeming important. He is the
/// west end's single Act-I dread tell, and he is NEVER threatened on screen. Voice:
/// soft, courteous, zero complaints. His room's lit window and parked sedan are
/// model-derived staging (MotelRules.LitRoom / OccupiedRooms — room 3).
/// Look: cast_west block 3 (charcoal sport coat; three weeks in and not one pixel
/// rumpled — the art contract's one tell for him).
/// </summary>
public static class Pell
{
    public const string Id = "pell";

    // Three weeks into a one-night stay. Mornings in the lobby, evenings in the
    // lobby; the hours between he walks the roads, which never take him anywhere.
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            120, 360,                                        // 8:00 AM - noon
            new NpcPlacement(MapIds.Motel, 8, 2, 0, Ambit: 2)),   // by the bench
        new(null, null,
            780, 1080,                                       // 7:00 PM - midnight
            new NpcPlacement(MapIds.Motel, 8, 2, 0, Ambit: 2)),
    };

    // The west end's one Act-I dread tell: a stranger who is far too content.
    // [KEVIN] invented copy
    public static readonly DialogueDef Default = Say.Linear("pell_default", Id,
        "Oh — hello! Lovely evening. Or morning. It does slip past me here. Isn't that funny.",
        "I sleep at this motel like I slept when I was a boy. Deeper, even. I keep meaning to push on — samples don't sell themselves — but every morning it seems to matter a little less.",
        "Isn't that a kind of mercy?");

    public static string? Talk(GameData data, GameTime now) => Default.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Mr. Pell", CastSheets.West, 3, Schedule), Talk, new[] { Default });
}
