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
            new ItemDef("turnip_seeds", "Turnip Seeds", ItemCategory.Seed, 99, 10, "#c8b060", PlantsCropId: "turnip"),
            new ItemDef("greenbean_seeds", "Green Bean Seeds", ItemCategory.Seed, 99, 15, "#7ab060", PlantsCropId: "greenbean"),
            new ItemDef("turnip", "Turnip", ItemCategory.Crop, 99, 35, "#d8c8e8"),
            new ItemDef("greenbean", "Green Bean", ItemCategory.Crop, 99, 40, "#4a9a4a"),
        };
        return defs.ToDictionary(d => d.Id);
    }
}
