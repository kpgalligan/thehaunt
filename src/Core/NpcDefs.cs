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
        // Intro cast display strings are role labels (names undecided) [KEVIN]; the
        // road-strip cast is named per docs/story/cast.md (2026-08-27 commission,
        // pending Kevin's review). Tunic colors [KEVIN].
        var defs = new[]
        {
            new NpcDef("mayor", "Mayor", "#8a4a7a", NpcSchedules.Mayor),
            new NpcDef("foreman", "Foreman", "#a0622e", NpcSchedules.Foreman),
            new NpcDef("crew_worker_a", "Repair Worker", "#3a6a9a", NpcSchedules.CrewWorkerA),
            new NpcDef("crew_worker_b", "Repair Worker", "#4a8a4a", NpcSchedules.CrewWorkerB),
            new NpcDef("shopkeeper", "Shopkeeper", "#b08a4a" /* [KEVIN] */, NpcSchedules.Shopkeeper),

            // The road strip (docs/story/cast.md).
            new NpcDef("walt", "Walt", "#6e6a58", NpcSchedules.Walt),
            new NpcDef("pell", "Mr. Pell", "#45454e", NpcSchedules.Pell),
            new NpcDef("dennis", "Dennis", "#a03a35", NpcSchedules.Dennis),
            new NpcDef("gloria", "Gloria", "#c25e8e", NpcSchedules.Gloria),
            new NpcDef("billie", "Billie", "#35564a", NpcSchedules.Billie),
            new NpcDef("bud", "Bud", "#6a7040", NpcSchedules.Bud),
            new NpcDef("pete", "Pete", "#8a7a9a", NpcSchedules.Pete),
            new NpcDef("moody", "Moody", "#b07a85", NpcSchedules.Moody),
            new NpcDef("lyle", "Lyle", "#557a8a", NpcSchedules.Lyle),
            new NpcDef("harriet", "Harriet", "#8f3a4a", NpcSchedules.Harriet),
            new NpcDef("ray", "Ray", "#4a4a6a", NpcSchedules.Ray),
            new NpcDef("nora", "Nora", "#d0b060", NpcSchedules.Nora),
            new NpcDef("sam", "Sam", "#60b0a0", NpcSchedules.Sam),
            new NpcDef("abe", "Abe", "#7a6a4a", NpcSchedules.Abe),
        };
        return defs.ToDictionary(d => d.Id);
    }
}
