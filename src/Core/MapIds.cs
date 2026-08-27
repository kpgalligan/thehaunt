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

    public static readonly IReadOnlyList<string> All = new[]
    {
        Farm, Town, TownHall, FarmHouse, GeneralStore, Barn,
        WestEntry, Billies, Fork, EastFork, EastEntry,
    };
}
