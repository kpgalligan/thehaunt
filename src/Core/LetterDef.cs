namespace TheHaunt.Core;

/// <summary>One line of a letter's package: an item id and how many of it.</summary>
public sealed record LetterItem(string ItemId, int Count);

/// <summary>
/// A piece of mail. Letters are CONTENT (LetterDefs registry) — the model keeps no
/// letter objects at all: delivery is derived per look (MailRules — RequiresFlag
/// plus FromDay, both monotone, so a delivered letter never disappears), reading
/// stamps <see cref="ReadFlag"/>, and a package pays out once under
/// <see cref="TakenFlag"/>. Both flags are StoryKeys constants (validation-tested).
/// <see cref="Items"/> and <see cref="TakenFlag"/> come together or not at all —
/// an info letter carries neither.
/// </summary>
public sealed record LetterDef(
    string Id,
    string Title,
    string Body,
    string ReadFlag,
    string? TakenFlag = null,
    IReadOnlyList<LetterItem>? Items = null,
    string? RequiresFlag = null,
    long FromDay = 0);
