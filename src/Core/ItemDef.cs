namespace TheHaunt.Core;

public enum ItemCategory { Tool, Seed, Crop, Material }

public enum ToolKind { Hoe, WateringCan, Scythe, Axe, Pick }

public sealed record ItemDef(
    string Id,
    string Name,
    ItemCategory Category,
    int MaxStack,
    int SellPrice,              // 0 = unsellable
    string IconColor,           // "#rrggbb" — the item's key color; feeds tools/gen_item_icons.py (edit both together, re-run it)
    ToolKind? Tool = null,
    int StaminaCost = 0,
    string? PlantsCropId = null);
