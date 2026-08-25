namespace TheHaunt.Core;

public sealed record DialogueLine(string SpeakerRole, string Text);           // "" = narration

public sealed record DialogueChoice(string Text, string NextNodeId, string? SetsFlag = null);

public sealed record DialogueNode(
    string Id,
    IReadOnlyList<DialogueLine> Lines,                // never empty (test-enforced)
    string? NextNodeId = null,                        // linear continuation
    IReadOnlyList<DialogueChoice>? Choices = null,    // shown with the LAST line; exclusive with NextNodeId (test-enforced)
    string? SetsFlag = null);                         // accumulated on node entry, applied at session end

public sealed record DialogueDef(string Id, string StartNodeId, IReadOnlyDictionary<string, DialogueNode> Nodes);
