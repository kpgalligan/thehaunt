namespace TheHaunt.Core;

public static class NpcDefs
{
    // Insertion order below is the canonical iteration order for All.
    public static IReadOnlyDictionary<string, NpcDef> All { get; } = Build();

    // Missing id here is a code bug — throws KeyNotFoundException.
    public static NpcDef Get(string id) => All[id];

    // Null-tolerant lookup for role ids coming from dialogue defs / views.
    public static NpcDef? TryGet(string id) => All.TryGetValue(id, out var def) ? def : null;

    private static Dictionary<string, NpcDef> Build()
    {
        // Display strings are role labels (names forbidden) [KEVIN]; tunic colors [KEVIN].
        var defs = new[]
        {
            new NpcDef("mayor", "Mayor", "#8a4a7a", NpcSchedules.Mayor),
            new NpcDef("foreman", "Foreman", "#a0622e", NpcSchedules.Foreman),
            new NpcDef("crew_worker_a", "Repair Worker", "#3a6a9a", NpcSchedules.CrewWorkerA),
            new NpcDef("crew_worker_b", "Repair Worker", "#4a8a4a", NpcSchedules.CrewWorkerB),
        };
        return defs.ToDictionary(d => d.Id);
    }
}
