namespace TheHaunt.Core;

public static class NpcDefs
{
    // The packed cast atlases (cast-sprites handoff, 2026-08-27): one 96x96 block
    // per character, block order fixed by the handoff. Per-character wardrobe lives
    // in the art; changing it is a gen_cast.js spec edit, never a code change.
    private const string Town = "res://assets/sprites/cast/cast_town.png";
    private const string West = "res://assets/sprites/cast/cast_west.png";
    private const string Billies = "res://assets/sprites/cast/cast_billies.png";
    private const string East = "res://assets/sprites/cast/cast_east.png";

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
        // pending Kevin's review).
        var defs = new[]
        {
            new NpcDef("mayor", "Mayor", Town, 0, NpcSchedules.Mayor),
            new NpcDef("foreman", "Foreman", Town, 1, NpcSchedules.Foreman),
            new NpcDef("crew_worker_a", "Repair Worker", Town, 2, NpcSchedules.CrewWorkerA),
            new NpcDef("crew_worker_b", "Repair Worker", Town, 3, NpcSchedules.CrewWorkerB),
            new NpcDef("shopkeeper", "Shopkeeper", Town, 4, NpcSchedules.Shopkeeper),

            // The road strip (docs/story/cast.md).
            new NpcDef("walt", "Walt", West, 0, NpcSchedules.Walt),
            new NpcDef("pell", "Mr. Pell", West, 3, NpcSchedules.Pell),
            new NpcDef("dennis", "Dennis", West, 1, NpcSchedules.Dennis),
            new NpcDef("gloria", "Gloria", West, 2, NpcSchedules.Gloria),
            new NpcDef("billie", "Billie", Billies, 0, NpcSchedules.Billie),
            new NpcDef("bud", "Bud", Billies, 1, NpcSchedules.Bud),
            new NpcDef("pete", "Pete", Billies, 2, NpcSchedules.Pete),
            new NpcDef("moody", "Moody", Billies, 3, NpcSchedules.Moody),
            new NpcDef("lyle", "Lyle", Billies, 4, NpcSchedules.Lyle),
            new NpcDef("harriet", "Harriet", Billies, 5, NpcSchedules.Harriet),
            new NpcDef("ray", "Ray", Billies, 6, NpcSchedules.Ray),
            new NpcDef("nora", "Nora", Billies, 7, NpcSchedules.Nora),
            new NpcDef("sam", "Sam", East, 0, NpcSchedules.Sam),
            new NpcDef("abe", "Abe", East, 1, NpcSchedules.Abe),

            // The garage clerk (name is Kevin's, 2026-08-30; wardrobe appended to
            // cast_west as block 4 via gen_cast.js — append-only, existing blocks
            // are fixed by the handoff README).
            new NpcDef("mike", "Mike", West, 4, NpcSchedules.Mike),
        };
        return defs.ToDictionary(d => d.Id);
    }
}
