namespace TheHaunt.Core;

public enum ItemCategory { Tool, Seed, Crop, Material }

public enum ToolKind { Hoe, WateringCan, Scythe }

public sealed record ItemDef(
    string Id,
    string Name,
    ItemCategory Category,
    int MaxStack,
    int SellPrice,              // 0 = unsellable
    string IconColor,           // "#rrggbb" — UI tints procedural icons with this
    ToolKind? Tool = null,
    int StaminaCost = 0,
    string? PlantsCropId = null);
