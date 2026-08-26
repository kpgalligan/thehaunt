namespace TheHaunt.Core;

public static class MapIds
{
    public const string Farm = "test_farm";   // rename to "farm" deferred to the first editor-authored map (its own migration)
    public const string Town = "town";
    public const string TownHall = "town_hall";
    public const string FarmHouse = "farm_house";
    public const string GeneralStore = "general_store";
    public const string Barn = "barn";
    public static readonly IReadOnlyList<string> All =
        new[] { Farm, Town, TownHall, FarmHouse, GeneralStore, Barn };
}
