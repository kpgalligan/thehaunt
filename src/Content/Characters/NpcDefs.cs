using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// DERIVED registry: the cast's NpcDefs in <see cref="Characters.All"/> order (the
/// canonical iteration order). A character is authored whole in its own file in
/// this folder; this view exists for consumers that need only identity + schedule
/// (WorldSim's sync, the map views, tests).
/// </summary>
public static class NpcDefs
{
    public static IReadOnlyDictionary<string, NpcDef> All { get; } =
        Characters.All.Select(c => c.Npc).ToDictionary(n => n.Id);

    /// <summary>Missing id here is a code bug — throws KeyNotFoundException.</summary>
    public static NpcDef Get(string id) => All[id];

    /// <summary>Null-tolerant lookup for role ids coming from dialogue defs / views.</summary>
    public static NpcDef? TryGet(string id) => All.TryGetValue(id, out var def) ? def : null;
}
