namespace TheHaunt.Core;

/// <summary>
/// The motor court's story-state reads (docs/designs/design_handoff_motel_signage):
/// four guest rooms that unlock individually, the night-time occupancy tell, and the
/// pole sign's NO circuit. Pure model reads — a flag's value in this model is the day
/// it was stamped, never a level (see <see cref="BarnRules"/>), which is why the
/// handoff's "int 1-4" occupancy state ships as a derivation instead.
/// </summary>
public static class MotelRules
{
    public const int Rooms = 4;

    public static string RoomFlag(int room) => room switch
    {
        1 => StoryKeys.MotelRoom1Open,
        2 => StoryKeys.MotelRoom2Open,
        3 => StoryKeys.MotelRoom3Open,
        4 => StoryKeys.MotelRoom4Open,
        _ => throw new ArgumentOutOfRangeException(nameof(room), room, "The motel has rooms 1-4."),
    };

    /// <summary>Rooms are locked by absence: no flag, no entry. Nothing in Act I sets
    /// any of these — the seam is deliberately empty, like the barn's.</summary>
    public static bool IsRoomOpen(GameData data, int room) => data.HasFlag(RoomFlag(room));

    /// <summary>
    /// Which room shows a lit window at night — the occupancy read lands before any
    /// dialogue does, so it is story state, not decoration. Room 3 is Pell's ("Fella
    /// in room three"). Nothing moves it yet; when guests change, this derivation
    /// grows flags, not an int.
    /// </summary>
    public static int LitRoom(GameData data) => 3;

    /// <summary>Circuit A: the NO tube lights only when the motel is full — expected
    /// false for all of Act I. The sign's whole characterisation is that it never lights.</summary>
    public static bool NoVacancy(GameData data) => data.HasFlag(StoryKeys.MotelFull);
}
