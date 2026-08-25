namespace TheHaunt.Core;

public readonly record struct OvernightReport(int CropsGrown, long ShippingProceeds);

// dayEnding = the DayEnded payload's DayIndex (the day being closed — NEVER "now",
// which is already next morning by the time views observe it).
public static class OvernightSim
{
    public static OvernightReport Run(GameData data, long dayEnding)
    {
        // 1. Growth — every MapState, loaded or not.
        int cropsGrown = 0;
        foreach (MapState map in data.Maps.Values)
        {
            foreach (TileRecord tile in map.Tiles)
            {
                if (tile.CropId is not string cropId || CropDefs.TryGet(cropId) is not CropDef def)
                {
                    continue;
                }
                if (tile.LastWateredDay == dayEnding && tile.GrowthDay < def.TotalDays)
                {
                    tile.GrowthDay++;
                    cropsGrown++;
                }
            }
        }

        // 2. Shipping sale: sold stacks removed; unknown or unsellable ids are
        //    SKIPPED and PRESERVED in the bin (item deletion is data loss).
        long proceeds = 0;
        var bin = data.ShippingBin;
        for (int i = bin.Count - 1; i >= 0; i--)
        {
            ItemDef? def = ItemDefs.TryGet(bin[i].ItemId);
            if (def is null || def.SellPrice <= 0)
            {
                continue;
            }
            proceeds += (long)def.SellPrice * bin[i].Count;
            bin.RemoveAt(i);
        }
        data.Player.Money += proceeds;

        // 3. Rest.
        data.Player.Stamina = data.Player.MaxStamina;

        return new OvernightReport(cropsGrown, proceeds);
    }
}
