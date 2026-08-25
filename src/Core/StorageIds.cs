namespace TheHaunt.Core;

public static class StorageIds
{
    public const string FarmHouseChest = "farm_house_chest";

    // Chest capacity is 20 (2 rows of 10, hotbar-width). [KEVIN] Growing it later
    // is a constant change here, never a migration. Unknown ids => null: storage
    // keys from newer saves round-trip un-padded (preserve-unknown rule).
    public static int? CapacityOf(string id) => id switch
    {
        FarmHouseChest => 20,
        _ => null,
    };
}
