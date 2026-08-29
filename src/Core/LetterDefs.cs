namespace TheHaunt.Core;

/// <summary>
/// The mail catalog. Insertion order below is the canonical iteration order for
/// <see cref="All"/> — the mailbox UI lists letters in this order.
/// </summary>
public static class LetterDefs
{
    public const string Farewell = "farewell";

    public static IReadOnlyDictionary<string, LetterDef> All { get; } = Build();

    /// <summary>Null-tolerant lookup for ids coming from callers that tolerate absence.</summary>
    public static LetterDef? TryGet(string id) => All.TryGetValue(id, out var def) ? def : null;

    private static Dictionary<string, LetterDef> Build()
    {
        var defs = new[]
        {
            // The farewell letter from the farmer who sold the land — waiting in the
            // mailbox from the first morning. Reading it starts the first-crops
            // quest (QuestDefs). Body is Kevin's copy verbatim (2026-08-28).
            // Title is [KEVIN]-provisional; the letter itself is deliberately unsigned.
            new LetterDef(
                Farewell,
                "From the previous owner",
                "I hope you enjoy your new farm. I know it's not much. I've allowed it to wither in my old age. But the land is fertile, and the people in town are the salt of the earth.\n\n"
                + "There is something about the town I haven't told you. I am sorry. But it is not a situation I've caused, and we all are forced to live with it. You will find out soon enough.\n\n"
                + "But, life can be good here. It is what you make of it. You'll find my farming tools in the barn, along with some seeds. Plant a few crops today. Till the soil, plant the seeds, then water them. Not that I need to tell you.\n\n"
                + "Best of luck. To you and me both. I go on to an unknown future. Your future I know all too well. I am sorry, but it can be what you make of it.",
                ReadFlag: StoryKeys.FarewellRead),
        };
        return defs.ToDictionary(d => d.Id);
    }
}
