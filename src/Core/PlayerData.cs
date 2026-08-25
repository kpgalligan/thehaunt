namespace TheHaunt.Core;

public sealed class PlayerData
{
    public string MapId { get; set; } = "test_farm";
    public float X { get; set; }
    public float Y { get; set; }
    public int Facing { get; set; }              // 0=down 1=left 2=right 3=up
    public bool HasPosition { get; set; }        // false until first WriteState; NaN is not JSON-safe
    public long Money { get; set; }
    public int Stamina { get; set; }
    public int MaxStamina { get; set; }
    public InventoryData Inventory { get; set; } = new();
}
