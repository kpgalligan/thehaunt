namespace TheHaunt.Content;

/// <summary>
/// The cast registry — one <see cref="CharacterDef"/> per character, each authored
/// whole in its own file (this folder). Insertion order below is the canonical
/// iteration order (unchanged from the pre-split NpcDefs order): the intro cast,
/// then the road strip west to east, then Mike, then the drive-in's summer cast.
///
/// A character file must never reference this class — the registry observes the
/// characters, and a cycle would hand static initialisation a half-built list.
/// </summary>
public static class Characters
{
    public static IReadOnlyList<CharacterDef> All { get; } = new[]
    {
        Mayor.Def, Foreman.Def, CrewWorkerA.Def, CrewWorkerB.Def, Shopkeeper.Def,
        Walt.Def, Pell.Def, Dennis.Def, Gloria.Def,
        Billie.Def, Bud.Def, Pete.Def, Moody.Def, Lyle.Def, Harriet.Def, Ray.Def, Nora.Def,
        Sam.Def, Abe.Def,
        Mike.Def,
        Shelly.Def,
    };

    private static readonly Dictionary<string, CharacterDef> ById =
        All.ToDictionary(c => c.Npc.Id);

    /// <summary>Missing id here is a code bug — throws KeyNotFoundException.</summary>
    public static CharacterDef Get(string id) => ById[id];

    /// <summary>Null-tolerant lookup for role ids coming from views and dialogue defs.</summary>
    public static CharacterDef? TryGet(string id) => ById.TryGetValue(id, out var def) ? def : null;
}
