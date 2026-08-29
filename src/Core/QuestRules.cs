namespace TheHaunt.Core;

/// <summary>
/// Pure quest-lifecycle derivations over the two flag stamps (QuestDef). A quest is
/// invisible until started; completion requires BOTH stamps, so a world event that
/// lands before the hand-out leaves the quest hidden until it is started — at which
/// point it appears already complete (and the start flag is what "completes" it,
/// which is why <see cref="CompletedBy"/> matches either flag). All functions are
/// TOTAL — hostile flag soups degrade, never throw.
/// </summary>
public static class QuestRules
{
    public static bool Started(QuestDef quest, GameData data) => data.HasFlag(quest.StartFlag);

    public static bool Completed(QuestDef quest, GameData data) =>
        data.HasFlag(quest.StartFlag) && data.HasFlag(quest.CompleteFlag);

    /// <summary>Started and still open — what the quest log's active section lists.</summary>
    public static bool Active(QuestDef quest, GameData data) =>
        Started(quest, data) && !data.HasFlag(quest.CompleteFlag);

    public static IReadOnlyList<QuestDef> ActiveQuests(GameData data) =>
        Where(data, Active);

    public static IReadOnlyList<QuestDef> CompletedQuests(GameData data) =>
        Where(data, Completed);

    /// <summary>
    /// The quests a freshly stamped flag just handed out (now active). Toast source:
    /// call AFTER the stamp lands, with the flag that landed.
    /// </summary>
    public static IReadOnlyList<QuestDef> StartedBy(string flagId, GameData data) =>
        Where(data, (q, d) => q.StartFlag == flagId && Active(q, d));

    /// <summary>
    /// The quests a freshly stamped flag just completed. Matches the quest's
    /// CompleteFlag in the usual order, and its StartFlag for the late-hand-out
    /// order (the world event happened first) — either way the quest is only
    /// reported once, on the stamp that made Completed true.
    /// </summary>
    public static IReadOnlyList<QuestDef> CompletedBy(string flagId, GameData data) =>
        Where(data, (q, d) => (q.CompleteFlag == flagId || q.StartFlag == flagId) && Completed(q, d));

    private static IReadOnlyList<QuestDef> Where(GameData data, Func<QuestDef, GameData, bool> keep)
    {
        var quests = new List<QuestDef>();
        foreach (QuestDef quest in QuestDefs.All.Values)
        {
            if (keep(quest, data))
            {
                quests.Add(quest);
            }
        }
        return quests;
    }
}
