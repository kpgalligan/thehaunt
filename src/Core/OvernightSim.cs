namespace TheHaunt.Core;

// Sales itemizes the sold stacks (one ShippedLine per SOLD stack; line proceeds
// sum to ShippingProceeds). The default keeps pre-3b constructor calls and
// property reads compiling.
public readonly record struct OvernightReport(int CropsGrown, long ShippingProceeds,
    IReadOnlyList<ShippedLine>? Sales = null);

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
        var sales = new List<ShippedLine>();
        var bin = data.ShippingBin;
        for (int i = bin.Count - 1; i >= 0; i--)
        {
            ItemDef? def = ItemDefs.TryGet(bin[i].ItemId);
            // Count <= 0 can only come from a hostile writer, but a negative count here
            // would MINT negative money — skip and preserve; load repair drops it later.
            if (def is null || def.SellPrice <= 0 || bin[i].Count <= 0)
            {
                continue;
            }
            long lineProceeds = (long)def.SellPrice * bin[i].Count;
            proceeds += lineProceeds;
            sales.Add(new ShippedLine(bin[i].ItemId, bin[i].Count, lineProceeds));
            bin.RemoveAt(i);
        }
        sales.Reverse();   // the removal loop walks backwards; report lines in deposit order
        data.Player.Money += proceeds;

        // 3. Rest.
        data.Player.Stamina = data.Player.MaxStamina;

        // 4. The scooter comes home (Kevin, 2026-08-27): wherever it was left — any
        //    map, any tile, even mid-ride in a hand-edited save — it is parked outside
        //    the farmhouse by morning. Never stolen, never lost overnight.
        data.Scooter = ScooterData.AtHome();

        return new OvernightReport(cropsGrown, proceeds, sales);
    }
}
