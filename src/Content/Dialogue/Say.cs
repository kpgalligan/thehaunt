using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Builders that keep dialogue authoring down to the words. <see cref="Linear"/> is
/// the ambient shape (one speaker, one node, a few lines); <see cref="Graph"/> is the
/// general form — hubs, choices, flags — taking its nodes verbatim. Dialogue ids stay
/// explicit at every call site: ids are forever (tests, the director and selectors
/// key on them), and a builder that invented them would hide that.
/// </summary>
public static class Say
{
    /// <summary>One linear node named "default", every line by one speaker.</summary>
    public static DialogueDef Linear(string id, string speaker, params string[] lines) =>
        Graph(id, "default", new DialogueNode("default",
            lines.Select(text => new DialogueLine(speaker, text)).ToList()));

    /// <summary>The general graph — same contract as DialogueDef's own constructor,
    /// with the node dictionary built from the nodes' ids.</summary>
    public static DialogueDef Graph(string id, string startNodeId, params DialogueNode[] nodes) =>
        new(id, startNodeId, nodes.ToDictionary(n => n.Id));
}
