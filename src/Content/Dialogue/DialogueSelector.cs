using TheHaunt.Core;

namespace TheHaunt.Content;

// Ambient-talk selection, DERIVED per character: each CharacterDef's Talk is the
// arm the old switch held, now authored beside the words it selects. Beat dialogues
// (IntroBeats) are started exclusively by StoryDirector — during their pending
// window the staged NPC's Talk returns null (present-but-silent ⇒ no Talk prompt).
public static class DialogueSelector
{
    // Total: unknown roles and hostile flag combinations return null, never throw.
    public static string? ForNpc(string roleId, GameData data, GameTime now) =>
        Characters.TryGet(roleId)?.Talk(data, now);
}
