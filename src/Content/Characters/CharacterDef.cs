using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>Which conversation a character offers right now — a pure read of
/// (flags, clock, model state); null = present-but-silent (no Talk prompt).</summary>
public delegate string? TalkSelector(GameData data, GameTime now);

/// <summary>
/// One cast member, whole: identity and look (<see cref="Npc"/> — sheet + block per
/// the cast-sprites handoff; wardrobe belongs to the art contract and is never
/// restated in code), where and when they stand (the NpcDef's schedule), which
/// conversation they offer (<see cref="Talk"/>), and the words themselves
/// (<see cref="Dialogues"/>). The mechanism registries — NpcDefs, DialogueDefs,
/// DialogueSelector — are DERIVED from <see cref="Characters.All"/>, so a character
/// is authored in one file and consumed everywhere.
/// </summary>
public sealed record CharacterDef(
    NpcDef Npc,
    TalkSelector Talk,
    IReadOnlyList<DialogueDef> Dialogues,
    bool SilentByDesign = false);
