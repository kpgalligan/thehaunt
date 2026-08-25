namespace TheHaunt.Core;

// Pure state machine over a DialogueDef; never serialized. Dialogue is atomic:
// complete or replay — an interrupted session leaves no trace in the model.
// Flags accumulate in FlagsRaised as nodes are entered / choices taken; the
// caller applies them only at session end.
public sealed class DialogueSession
{
    private readonly List<string> _flagsRaised = new();
    private DialogueNode _node;
    private int _lineIndex;

    public DialogueSession(DialogueDef def)
    {
        Def = def;
        _node = def.Nodes[def.StartNodeId];
        Accumulate(_node.SetsFlag);
    }

    public DialogueDef Def { get; }

    public bool Finished { get; private set; }

    // After Finished this stays on the terminal node's last line — never throws.
    public DialogueLine CurrentLine => _node.Lines[_lineIndex];

    public bool AtChoices =>
        !Finished && _node.Choices is { Count: > 0 } && _lineIndex == _node.Lines.Count - 1;

    public IReadOnlyList<DialogueChoice> CurrentChoices =>
        AtChoices ? _node.Choices! : Array.Empty<DialogueChoice>();

    // Node-entry + chosen-choice flags, in order.
    public IReadOnlyList<string> FlagsRaised => _flagsRaised;

    // False when AtChoices or Finished. On the last line of a linear node:
    // enters NextNodeId (accumulating its SetsFlag) or sets Finished when null.
    public bool Advance()
    {
        if (Finished || AtChoices)
        {
            return false;
        }
        if (_lineIndex < _node.Lines.Count - 1)
        {
            _lineIndex++;
            return true;
        }
        if (_node.NextNodeId is null)
        {
            Finished = true;
            return true;
        }
        EnterNode(_node.NextNodeId);
        return true;
    }

    // Only legal when AtChoices; range-checked (false otherwise). Accumulates the
    // choice's SetsFlag, then the target node's, and resets to its line 0.
    public bool Choose(int index)
    {
        if (!AtChoices)
        {
            return false;
        }
        var choices = _node.Choices!;
        if (index < 0 || index >= choices.Count)
        {
            return false;
        }
        Accumulate(choices[index].SetsFlag);
        EnterNode(choices[index].NextNodeId);
        return true;
    }

    private void EnterNode(string nodeId)
    {
        _node = Def.Nodes[nodeId];
        _lineIndex = 0;
        Accumulate(_node.SetsFlag);
    }

    private void Accumulate(string? flag)
    {
        if (flag != null)
        {
            _flagsRaised.Add(flag);
        }
    }
}
