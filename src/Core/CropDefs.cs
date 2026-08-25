namespace TheHaunt.Core;

public static class CropDefs
{
    // Insertion order below (turnip, then greenbean) is the canonical iteration
    // order for All — the Crops atlas assigns one row per CropDef in this order.
    public static IReadOnlyDictionary<string, CropDef> All { get; } = Build();

    // Missing id here is a code bug — throws KeyNotFoundException.
    public static CropDef Get(string id) => All[id];

    // Null-tolerant lookup for ids coming from save files.
    public static CropDef? TryGet(string id) => All.TryGetValue(id, out var def) ? def : null;

    private static Dictionary<string, CropDef> Build()
    {
        var defs = new[]
        {
            new CropDef("turnip", "Turnip", new[] { 1, 1, 1, 2 }, "turnip"),
            new CropDef("greenbean", "Green Bean", new[] { 1, 1, 2, 2 }, "greenbean", RegrowDays: 3),
            // Ratified 3b economy additions: potato is the first HarvestCount user;
            // cauliflower is the premium slow single.
            new CropDef("potato", "Potato", new[] { 1, 1, 2, 2 }, "potato", HarvestCount: 2),
            new CropDef("cauliflower", "Cauliflower", new[] { 2, 2, 3, 3 }, "cauliflower"),
        };
        return defs.ToDictionary(d => d.Id);
    }
}
