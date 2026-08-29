namespace TheHaunt.Core;

/// <summary>
/// The quest catalog. Insertion order below is the canonical iteration order for
/// <see cref="All"/> — the quest log lists quests in this order.
/// </summary>
public static class QuestDefs
{
    public const string FirstCrops = "first_crops";

    public static IReadOnlyDictionary<string, QuestDef> All { get; } = Build();

    /// <summary>Null-tolerant lookup for ids coming from callers that tolerate absence.</summary>
    public static QuestDef? TryGet(string id) => All.TryGetValue(id, out var def) ? def : null;

    private static Dictionary<string, QuestDef> Build()
    {
        var defs = new[]
        {
            // Handed out by the farewell letter in the mailbox; done when a watering
            // lands on a planted tile (the letter's own ask, word for word). Title is
            // [KEVIN]-provisional; the description is lifted from Kevin's letter copy.
            new QuestDef(
                FirstCrops,
                "Plant a Few Crops",
                "Till the soil, plant the seeds, then water them.",
                StartFlag: StoryKeys.FarewellRead,
                CompleteFlag: StoryKeys.FirstWatering),
        };
        return defs.ToDictionary(d => d.Id);
    }
}
