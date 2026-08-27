namespace TheHaunt.Core;

public static class MapIds
{
    public const string Farm = "test_farm";   // rename to "farm" deferred to the first editor-authored map (its own migration)
    public const string Town = "town";
    public const string TownHall = "town_hall";
    public const string FarmHouse = "farm_house";
    public const string GeneralStore = "general_store";
    public const string Barn = "barn";

    // The road frames, west to east: west_entry, billies, fork, town, east_fork,
    // east_entry (docs/story/README.md). The fork also runs north to the farm; the
    // two entry maps' outward mouths wire through RoadWrap.
    public const string WestEntry = "west_entry";
    public const string Billies = "billies";
    public const string Fork = "fork";
    public const string EastFork = "east_fork";
    public const string EastEntry = "east_entry";

    // The road strip's interiors (docs/story/cast.md): the motel lobby and the gas
    // station shop on the west entry, the bar room behind Billie's, and Sam's salon
    // on the east entry.
    public const string Motel = "motel";
    public const string GasStation = "gas_station";
    public const string BilliesBar = "billies_bar";
    public const string Salon = "salon";

    // The motor court's guest rooms (docs/designs/design_handoff_motel_signage): each
    // room is its own map and its own unlock flag, so story can grant access in any
    // order. Rooms are NOT one map with a variant parameter.
    public const string MotelRoom1 = "motel_room_1";
    public const string MotelRoom2 = "motel_room_2";
    public const string MotelRoom3 = "motel_room_3";
    public const string MotelRoom4 = "motel_room_4";

    // The dead drive-in theater, off the road south of the east fork
    // (docs/story/README.md). Jane's long-running refurbishment goal lives here later.
    public const string DriveIn = "drive_in";

    public static string MotelRoom(int room) => room switch
    {
        1 => MotelRoom1,
        2 => MotelRoom2,
        3 => MotelRoom3,
        4 => MotelRoom4,
        _ => throw new ArgumentOutOfRangeException(nameof(room), room, "The motel has rooms 1-4."),
    };

    // Which maps are interiors — mirrors each map class's IsInterior, and the drift
    // guard (Scooter_InteriorTableMatchesTheMaps) fails if a new map forgets to keep
    // them in step. Core needs this because the scooter's never-ridden-indoors rule
    // is enforced at load repair, where no map node exists to ask.
    private static readonly HashSet<string> Interiors = new()
    {
        TownHall, FarmHouse, GeneralStore, Barn,
        Motel, GasStation, BilliesBar, Salon,
        MotelRoom1, MotelRoom2, MotelRoom3, MotelRoom4,
    };

    /// <summary>False for unknown ids — outdoors is the safe default for a map this build cannot name.</summary>
    public static bool IsInterior(string mapId) => Interiors.Contains(mapId);

    public static readonly IReadOnlyList<string> All = new[]
    {
        Farm, Town, TownHall, FarmHouse, GeneralStore, Barn,
        WestEntry, Billies, Fork, EastFork, EastEntry,
        Motel, GasStation, BilliesBar, Salon,
        MotelRoom1, MotelRoom2, MotelRoom3, MotelRoom4, DriveIn,
    };
}
