using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// DERIVED registry: every conversation in the game — each character's dialogues
/// (in <see cref="Characters.All"/> order) plus the intro beats. The words are
/// AUTHORED in the character and arc files; this view exists for the session
/// starter (WorldSim.StartDialogue) and validation. Tone law for all of it: Act I
/// dread lives only in dialogue, only at look-twice-and-move-on strength
/// (src/Content/CLAUDE.md, Writing rules).
/// </summary>
public static class DialogueDefs
{
    public static IReadOnlyDictionary<string, DialogueDef> All { get; } =
        Characters.All.SelectMany(c => c.Dialogues)
            .Concat(IntroBeats.All)
            .ToDictionary(d => d.Id);

    /// <summary>Missing id here is a code bug — throws KeyNotFoundException.</summary>
    public static DialogueDef Get(string id) => All[id];

    /// <summary>Null-tolerant lookup for ids coming from callers that tolerate absence.</summary>
    public static DialogueDef? TryGet(string id) => All.TryGetValue(id, out var def) ? def : null;
}
