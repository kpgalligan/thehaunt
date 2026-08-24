namespace TheHaunt.Core;

public sealed class TileRecord
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Kind { get; set; } = "";       // e.g. "tilled" later; sparse deltas only
    public string? CropId { get; set; }
    public int GrowthDay { get; set; }
    public long LastWateredDay { get; set; } = -1;  // day-index, -1 = never. NOT a bool — survives skipped days.
}
