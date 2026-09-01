namespace TheHaunt.Core;

public sealed class PlacedObjectRecord
{
    public int X { get; set; }
    public int Y { get; set; }
    public string ObjectId { get; set; } = "";

    // Damage accumulated by tool strikes (ObstacleDefs); absent in older saves and
    // meaningless for non-obstacle objects, so 0 is the natural default either way.
    public int HitsTaken { get; set; }
}
