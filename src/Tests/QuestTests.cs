using System.Reflection;
using TheHaunt.Core;

namespace TheHaunt.Tests;

public static class QuestTests
{
    [SimTest]
    public static void Quest_RulesDeriveLifecycle(TestContext t)
    {
        QuestDef quest = QuestDefs.All[QuestDefs.FirstCrops];

        // Fresh save: invisible — not started, not active, not completed.
        var fresh = new GameData();
        t.Assert(!QuestRules.Started(quest, fresh), "fresh: not started");
        t.Assert(!QuestRules.Active(quest, fresh), "fresh: not active");
        t.Assert(!QuestRules.Completed(quest, fresh), "fresh: not completed");
        t.AssertEqual(0, QuestRules.ActiveQuests(fresh).Count, "fresh: nothing active");
        t.AssertEqual(0, QuestRules.CompletedQuests(fresh).Count, "fresh: nothing completed");

        // Usual order: start flag then complete flag.
        var usual = new GameData();
        usual.TrySetFlag(quest.StartFlag, 1);
        t.Assert(QuestRules.Active(quest, usual), "started: active");
        t.Assert(!QuestRules.Completed(quest, usual), "started: not completed yet");
        t.AssertEqual(1, QuestRules.ActiveQuests(usual).Count, "started: listed active");
        t.AssertEqual(1, QuestRules.StartedBy(quest.StartFlag, usual).Count,
            "the start stamp reports the hand-out");
        t.AssertEqual(0, QuestRules.CompletedBy(quest.StartFlag, usual).Count,
            "the start stamp completes nothing while the quest is open");
        usual.TrySetFlag(quest.CompleteFlag, 2);
        t.Assert(!QuestRules.Active(quest, usual), "completed: no longer active");
        t.Assert(QuestRules.Completed(quest, usual), "completed: completed");
        t.AssertEqual(1, QuestRules.CompletedBy(quest.CompleteFlag, usual).Count,
            "the complete stamp reports the completion");
        t.AssertEqual(0, QuestRules.StartedBy(quest.StartFlag, usual).Count,
            "a completed quest is never re-reported as handed out");

        // Late hand-out: the world event landed BEFORE the quest was started. The
        // quest stays hidden, then appears already complete — and it is the START
        // stamp that reports the completion (the only new stamp there is).
        var late = new GameData();
        late.TrySetFlag(quest.CompleteFlag, 1);
        t.Assert(!QuestRules.Started(quest, late), "late order: hidden before hand-out");
        t.Assert(!QuestRules.Completed(quest, late), "late order: not completed while hidden");
        t.AssertEqual(0, QuestRules.CompletedBy(quest.CompleteFlag, late).Count,
            "the early world event reports nothing");
        late.TrySetFlag(quest.StartFlag, 3);
        t.Assert(QuestRules.Completed(quest, late), "late order: born completed");
        t.Assert(!QuestRules.Active(quest, late), "late order: never active");
        t.AssertEqual(0, QuestRules.StartedBy(quest.StartFlag, late).Count,
            "born-completed is not a hand-out");
        t.AssertEqual(1, QuestRules.CompletedBy(quest.StartFlag, late).Count,
            "the late start stamp reports the completion");

        // Totality: hostile flag soups (unknown keys, unrelated flags) never throw.
        var hostile = new GameData();
        hostile.TrySetFlag("future.mystery_flag", 0);
        hostile.TrySetFlag(StoryKeys.MeetingDone, 999999);
        t.AssertEqual(0, QuestRules.ActiveQuests(hostile).Count, "hostile: nothing active");
        t.AssertEqual(0, QuestRules.StartedBy("future.mystery_flag", hostile).Count,
            "hostile: unknown flags start nothing");
        t.AssertEqual(0, QuestRules.CompletedBy("future.mystery_flag", hostile).Count,
            "hostile: unknown flags complete nothing");
    }

    [SimTest]
    public static void Quest_DefsValidate(TestContext t)
    {
        // The only legal flag ids in code are the StoryKeys constants (the dialogue
        // validation's rule, applied to the quest catalog).
        HashSet<string> legalFlags = typeof(StoryKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        t.Assert(QuestDefs.All.Count >= 1, "the catalog is not empty");
        foreach ((string id, QuestDef quest) in QuestDefs.All)
        {
            t.AssertEqual(id, quest.Id, $"quest '{id}': key matches Id");
            t.Assert(quest.Title.Length > 0, $"quest '{id}': Title non-empty");
            t.Assert(quest.Description.Length > 0, $"quest '{id}': Description non-empty");
            t.Assert(legalFlags.Contains(quest.StartFlag),
                $"quest '{id}': StartFlag is a StoryKeys constant");
            t.Assert(legalFlags.Contains(quest.CompleteFlag),
                $"quest '{id}': CompleteFlag is a StoryKeys constant");
            t.Assert(quest.StartFlag != quest.CompleteFlag,
                $"quest '{id}': StartFlag and CompleteFlag differ");
        }

        // The shipped hand-out chain: the farewell letter starts first_crops, and a
        // watering on a planted tile completes it.
        QuestDef firstCrops = QuestDefs.All[QuestDefs.FirstCrops];
        t.AssertEqual(StoryKeys.FarewellRead, firstCrops.StartFlag,
            "first_crops is handed out by reading the farewell letter");
        t.AssertEqual(StoryKeys.FirstWatering, firstCrops.CompleteFlag,
            "first_crops completes on the first crop watering");
    }
}
