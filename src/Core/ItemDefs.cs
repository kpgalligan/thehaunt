namespace TheHaunt.Core;

public static class ItemDefs
{
    // Insertion order below is the canonical iteration order for All.
    public static IReadOnlyDictionary<string, ItemDef> All { get; } = Build();

    // Missing id here is a code bug — throws KeyNotFoundException.
    public static ItemDef Get(string id) => All[id];

    // Null-tolerant lookup for ids coming from save files.
    public static ItemDef? TryGet(string id) => All.TryGetValue(id, out var def) ? def : null;

    private static Dictionary<string, ItemDef> Build()
    {
        var defs = new[]
        {
            new ItemDef("hoe", "Hoe", ItemCategory.Tool, 1, 0, "#8a5a3a", ToolKind.Hoe, StaminaCost: 2),
            new ItemDef("watering_can", "Watering Can", ItemCategory.Tool, 1, 0, "#6a8ab0", ToolKind.WateringCan, StaminaCost: 1),
            new ItemDef("scythe", "Scythe", ItemCategory.Tool, 1, 0, "#9a9a9a", ToolKind.Scythe, StaminaCost: 1),
            // Axe and pickaxe (tools handoff): work the field obstacles —
            // FarmActions' obstacle branch, ObstacleDefs for hits and yields.
            new ItemDef("axe", "Axe", ItemCategory.Tool, 1, 0, "#7a6a5c", ToolKind.Axe, StaminaCost: 2),
            new ItemDef("pick", "Pickaxe", ItemCategory.Tool, 1, 0, "#575a58", ToolKind.Pick, StaminaCost: 2),
            // Seed SellPrice is always half the shop buy price (ratified rule; the
            // Shop_SeedResaleIsHalfBuy invariant test enforces it).
            new ItemDef("turnip_seeds", "Turnip Seeds", ItemCategory.Seed, 99, 10, "#c8b060", PlantsCropId: "turnip"),
            new ItemDef("greenbean_seeds", "Green Bean Seeds", ItemCategory.Seed, 99, 30, "#7ab060", PlantsCropId: "greenbean"),
            new ItemDef("potato_seeds", "Potato Seeds", ItemCategory.Seed, 99, 25, "#b08d57", PlantsCropId: "potato"),
            new ItemDef("cauliflower_seeds", "Cauliflower Seeds", ItemCategory.Seed, 99, 40, "#c8d0a8", PlantsCropId: "cauliflower"),
            new ItemDef("turnip", "Turnip", ItemCategory.Crop, 99, 40, "#d8c8e8"),
            new ItemDef("greenbean", "Green Bean", ItemCategory.Crop, 99, 40, "#4a9a4a"),
            new ItemDef("potato", "Potato", ItemCategory.Crop, 99, 40, "#c9a86a"),
            new ItemDef("cauliflower", "Cauliflower", ItemCategory.Crop, 99, 175, "#e8e8d8"),
            // Field-clearing yields (ObstacleDefs): trees and stumps drop lumber,
            // rocks drop stone. Cheap to sell on purpose — they are building
            // materials first, and the crafting that spends them lands later.
            new ItemDef("lumber", "Lumber", ItemCategory.Material, 99, 2, "#8a6a42"),
            new ItemDef("stone", "Stone", ItemCategory.Material, 99, 2, "#8d8f8a"),
        };
        return defs.ToDictionary(d => d.Id);
    }
}
