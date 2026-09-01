using System.Reflection;
using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.UI;

namespace TheHaunt.Tests;

public static class DialogueTests
{
    [SimTest]
    public static void Dialogue_DefsValidate(TestContext t)
    {
        // The only legal flag ids in code are the StoryKeys constants (spec §1.2).
        HashSet<string> legalFlags = typeof(StoryKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();
        t.Assert(legalFlags.Count >= 4, "StoryKeys exposes the four intro constants");

        // The §3.2 catalog must resolve — DialogueSelector and StoryDirector return these ids.
        foreach (string id in new[]
        {
            "intro_crew_arrival", "intro_town_meeting", "intro_town_meeting_overslept",
            "foreman_wait", "foreman_after", "mayor_after", "crew_worker_default",
        })
        {
            t.Assert(DialogueDefs.TryGet(id) != null, $"catalog dialogue '{id}' resolves");
        }

        foreach (DialogueDef def in DialogueDefs.All.Values)
        {
            t.Assert(def.Nodes.ContainsKey(def.StartNodeId),
                $"dialogue '{def.Id}': StartNodeId '{def.StartNodeId}' resolves");

            var reachable = new HashSet<string>();
            var frontier = new Queue<string>();
            frontier.Enqueue(def.StartNodeId);
            reachable.Add(def.StartNodeId);

            foreach ((string nodeId, DialogueNode node) in def.Nodes)
            {
                string label = $"dialogue '{def.Id}' node '{nodeId}'";
                t.Assert(node.Lines is { Count: > 0 }, $"{label}: Lines never empty");
                bool hasChoices = node.Choices is { Count: > 0 };
                t.Assert(!(node.NextNodeId != null && hasChoices),
                    $"{label}: NextNodeId and Choices are exclusive");
                if (node.NextNodeId != null)
                {
                    t.Assert(def.Nodes.ContainsKey(node.NextNodeId),
                        $"{label}: NextNodeId '{node.NextNodeId}' resolves");
                }
                if (node.SetsFlag != null)
                {
                    t.Assert(legalFlags.Contains(node.SetsFlag),
                        $"{label}: SetsFlag '{node.SetsFlag}' is a StoryKeys constant");
                }
                if (hasChoices)
                {
                    foreach (DialogueChoice choice in node.Choices!)
                    {
                        t.Assert(def.Nodes.ContainsKey(choice.NextNodeId),
                            $"{label}: choice target '{choice.NextNodeId}' resolves");
                        if (choice.SetsFlag != null)
                        {
                            t.Assert(legalFlags.Contains(choice.SetsFlag),
                                $"{label}: choice SetsFlag '{choice.SetsFlag}' is a StoryKeys constant");
                        }
                    }
                }
                foreach (DialogueLine line in node.Lines)
                {
                    if (line.SpeakerRole.Length > 0)
                    {
                        t.Assert(NpcDefs.TryGet(line.SpeakerRole) != null,
                            $"{label}: speaker role '{line.SpeakerRole}' resolves in NpcDefs");
                    }
                }
            }

            while (frontier.Count > 0)
            {
                DialogueNode node = def.Nodes[frontier.Dequeue()];
                foreach (string? target in Targets(node))
                {
                    if (target != null && reachable.Add(target))
                    {
                        frontier.Enqueue(target);
                    }
                }
            }
            foreach (string nodeId in def.Nodes.Keys)
            {
                t.Assert(reachable.Contains(nodeId),
                    $"dialogue '{def.Id}': node '{nodeId}' reachable from the start node");
            }
        }

        // Every terminal path of each beat dialogue must raise its completion flag —
        // the beat machine relies on the terminal stamp to not replay forever.
        AssertTerminalsSetFlag(t, DialogueDefs.Get("intro_crew_arrival"), StoryKeys.CrewArrivalDone);
        AssertTerminalsSetFlag(t, DialogueDefs.Get("intro_town_meeting"), StoryKeys.MeetingDone);
        AssertTerminalsSetFlag(t, DialogueDefs.Get("intro_town_meeting_overslept"), StoryKeys.MeetingDone);
    }

    [SimTest]
    public static void Dialogue_SessionWalkAndBranch(TestContext t)
    {
        // A locally built def pins the machine's exact semantics independent of the
        // shipped copy: linear walk, a 2-way fork, convergence, flag accumulation order.
        var nodes = new Dictionary<string, DialogueNode>
        {
            ["start"] = new DialogueNode("start",
                new[] { new DialogueLine("", "a"), new DialogueLine("foreman", "b") },
                NextNodeId: "fork", SetsFlag: "test.entered"),
            ["fork"] = new DialogueNode("fork",
                new[] { new DialogueLine("mayor", "pick") },
                Choices: new[]
                {
                    new DialogueChoice("left", "end", "test.left"),
                    new DialogueChoice("right", "end"),
                }),
            ["end"] = new DialogueNode("end",
                new[] { new DialogueLine("", "done") }, SetsFlag: "test.done"),
        };
        var def = new DialogueDef("test_dlg", "start", nodes);

        var session = new DialogueSession(def);
        t.AssertEqual(new DialogueLine("", "a"), session.CurrentLine, "first line on entry");
        t.Assert(!session.AtChoices, "not at choices on the first line");
        t.Assert(!session.Finished, "not finished on entry");
        t.Assert(session.FlagsRaised.SequenceEqual(new[] { "test.entered" }),
            "start node's SetsFlag accumulated on entry");
        t.Assert(!session.Choose(0), "Choose refused outside a choices state");

        t.Assert(session.Advance(), "advance to line 2");
        t.AssertEqual(new DialogueLine("foreman", "b"), session.CurrentLine, "second line");
        t.Assert(session.Advance(), "advance into the fork node");
        t.AssertEqual(new DialogueLine("mayor", "pick"), session.CurrentLine, "fork line");
        t.Assert(session.AtChoices, "AtChoices on the last line of a Choices node");
        t.AssertEqual(2, session.CurrentChoices.Count, "both choices offered");
        t.Assert(!session.Advance(), "Advance false at choices");
        t.Assert(!session.Choose(-1), "Choose(-1) range-checked");
        t.Assert(!session.Choose(2), "Choose(2) range-checked");
        t.Assert(session.AtChoices, "refused chooses leave the state unchanged");

        t.Assert(session.Choose(0), "left fork");
        t.AssertEqual(new DialogueLine("", "done"), session.CurrentLine, "converged end node");
        t.Assert(!session.Finished, "end node still shows its line before finishing");
        t.Assert(session.FlagsRaised.SequenceEqual(
            new[] { "test.entered", "test.left", "test.done" }),
            "exact FlagsRaised, in accumulation order (node entry + chosen choice)");
        t.Assert(session.Advance(), "advancing past the last line finishes");
        t.Assert(session.Finished, "session finished");
        t.Assert(!session.Advance(), "Advance false once finished");
        t.Assert(session.CurrentChoices.Count == 0, "no choices once finished");

        // The right fork converges on the same end node without the choice flag.
        var other = new DialogueSession(def);
        t.Assert(other.Advance() && other.Advance(), "walk to the fork");
        t.Assert(other.Choose(1), "right fork");
        t.AssertEqual(new DialogueLine("", "done"), other.CurrentLine, "right fork converges");
        t.Assert(other.FlagsRaised.SequenceEqual(new[] { "test.entered", "test.done" }),
            "flagless choice adds nothing to FlagsRaised");
    }

    [SimTest]
    public static async Task Dialogue_WorldSimFlow(TestContext t)
    {
        SaveService service = SaveService.Instance;
        DialogueUi? ui = null;
        var flagsViaBus = new List<string>();
        Action<string, long> onFlagSet = (flagId, _) => flagsViaBus.Add(flagId);
        WorldSim.Instance.StoryFlagSet += onFlagSet;
        try
        {
            service.NewGame();
            ui = new DialogueUi();
            t.Host.AddChild(ui);
            await t.WaitFrames(1);
            t.Assert(!ui.Visible, "dialogue box hidden by default");

            WorldSim.Instance.AdvanceDialogue(); // safe no-op with no session
            t.Assert(WorldSim.Instance.ActiveDialogue == null, "no session after the no-op");

            t.Assert(WorldSim.Instance.StartDialogue("intro_crew_arrival"), "start returns true");
            t.Assert(!GameState.Instance.ClockRuns, "clock frozen while the dialogue runs");
            await t.WaitFrames(1);
            t.Assert(ui.Visible, "dialogue box visible after start");

            // No opening-press double-fire: one frame after start, line 1 is still current.
            DialogueDef def = DialogueDefs.Get("intro_crew_arrival");
            t.AssertEqual(def.Nodes[def.StartNodeId].Lines[0],
                WorldSim.Instance.ActiveDialogue!.CurrentLine,
                "first line still current after start");

            await DriveDialogueToCompletion(t, "crew arrival via the bus");
            t.Assert(service.Current.HasFlag(StoryKeys.CrewArrivalDone),
                "terminal flag applied at session end");
            t.Assert(flagsViaBus.Contains(StoryKeys.CrewArrivalDone),
                "flag application went through the bus (StoryFlagSet fired)");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "phase restored to Playing (dialogue started from Playing)");
            t.Assert(WorldSim.Instance.ActiveDialogue == null, "session nulled after finish");
            await t.WaitFrames(1);
            t.Assert(!ui.Visible, "dialogue box hidden after finish");

            // AfterLoad force-hides mid-session, applying nothing.
            t.Assert(WorldSim.Instance.StartDialogue("foreman_wait"), "second dialogue starts");
            await t.WaitFrames(1);
            t.Assert(ui.Visible, "dialogue box visible for the second session");
            service.NewGame(); // fires AfterLoad
            t.Assert(WorldSim.Instance.ActiveDialogue == null, "AfterLoad nulled the session");
            t.Assert(GameState.Instance.PlayerHasControl,
                "AfterLoad restored control for a Playing-started session (no Dialogue-phase strand)");
            await t.WaitFrames(1);
            t.Assert(!ui.Visible, "AfterLoad force-hid the dialogue box");
        }
        finally
        {
            WorldSim.Instance.StoryFlagSet -= onFlagSet;
            if (ui != null && GodotObject.IsInstanceValid(ui))
            {
                ui.Free();
            }
            await t.WaitFrames(1);
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    private static IEnumerable<string?> Targets(DialogueNode node)
    {
        yield return node.NextNodeId;
        if (node.Choices != null)
        {
            foreach (DialogueChoice choice in node.Choices)
            {
                yield return choice.NextNodeId;
            }
        }
    }

    // DFS over (node, flag-raised) states — memoized so hub-and-spoke cycles terminate.
    // Proves: every path that reaches a terminal node has raised the completion flag.
    private static void AssertTerminalsSetFlag(TestContext t, DialogueDef def, string completionFlag)
    {
        var visited = new HashSet<(string NodeId, bool Raised)>();
        var stack = new Stack<(string NodeId, bool Raised)>();
        stack.Push((def.StartNodeId, false));
        while (stack.Count > 0)
        {
            (string nodeId, bool raisedBefore) = stack.Pop();
            DialogueNode node = def.Nodes[nodeId];
            bool raised = raisedBefore || node.SetsFlag == completionFlag;
            if (!visited.Add((nodeId, raised)))
            {
                continue;
            }
            bool hasChoices = node.Choices is { Count: > 0 };
            if (node.NextNodeId == null && !hasChoices)
            {
                t.Assert(raised, $"dialogue '{def.Id}': terminal node '{nodeId}' " +
                    $"reachable without '{completionFlag}'");
                continue;
            }
            if (node.NextNodeId != null)
            {
                stack.Push((node.NextNodeId, raised));
            }
            if (hasChoices)
            {
                foreach (DialogueChoice choice in node.Choices!)
                {
                    stack.Push((choice.NextNodeId, raised || choice.SetsFlag == completionFlag));
                }
            }
        }
    }

    // Drives the active dialogue to completion from the outside, one pump per frame.
    // Choices are picked round-robin per node, so hub-and-spoke graphs (the town
    // meeting) reach their exit choice wherever the copy puts it.
    private static async Task DriveDialogueToCompletion(TestContext t, string label)
    {
        var visits = new Dictionary<string, int>();
        for (int step = 0; step < 400 && WorldSim.Instance.ActiveDialogue != null; step++)
        {
            DialogueSession session = WorldSim.Instance.ActiveDialogue;
            if (session.AtChoices)
            {
                string node = string.Join("|", session.CurrentChoices.Select(c => c.NextNodeId));
                int seen = visits.GetValueOrDefault(node);
                visits[node] = seen + 1;
                WorldSim.Instance.ChooseDialogueOption(seen % session.CurrentChoices.Count);
            }
            else
            {
                WorldSim.Instance.AdvanceDialogue();
            }
            await t.WaitFrames(1);
        }
        t.Assert(WorldSim.Instance.ActiveDialogue == null, $"{label}: dialogue ran to completion");
    }
}
