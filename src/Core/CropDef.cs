namespace TheHaunt.Core;

public sealed record CropDef(
    string Id,
    string Name,
    int[] StageDays,
    string HarvestItemId,
    int HarvestCount = 1,
    int RegrowDays = 0)         // 0 = single-harvest
{
    public int TotalDays { get; } = StageDays.Sum();

    // Stage s covers growth days [prefixSum(s), prefixSum(s+1)).
    // growthDay >= TotalDays returns StageDays.Length — the mature column.
    // Example, StageDays {1,1,1,2} (TotalDays 5): g0→0, g1→1, g2→2, g3→3, g4→3, g5→4.
    public int StageForDay(int growthDay)
    {
        if (growthDay >= TotalDays)
        {
            return StageDays.Length;
        }
        int prefix = 0;
        for (int s = 0; s < StageDays.Length; s++)
        {
            prefix += StageDays[s];
            if (growthDay < prefix)
            {
                return s;
            }
        }
        return StageDays.Length;
    }
}
