namespace TheHaunt.Core;

/// <summary>
/// Pure mail derivations. The model keeps NO mail state beyond story flags: a
/// letter is delivered when its monotone conditions hold (so mail never vanishes),
/// read when its ReadFlag is stamped, and its package is spent when its TakenFlag
/// is. All functions are TOTAL — hostile flag soups degrade, never throw.
/// </summary>
public static class MailRules
{
    public static bool IsDelivered(LetterDef letter, GameData data, GameTime now) =>
        (letter.RequiresFlag == null || data.HasFlag(letter.RequiresFlag))
        && now.DayIndex >= letter.FromDay;

    /// <summary>Every delivered letter, in LetterDefs order — the mailbox's contents.</summary>
    public static IReadOnlyList<LetterDef> Delivered(GameData data, GameTime now)
    {
        var letters = new List<LetterDef>();
        foreach (LetterDef letter in LetterDefs.All.Values)
        {
            if (IsDelivered(letter, data, now))
            {
                letters.Add(letter);
            }
        }
        return letters;
    }

    public static bool IsRead(LetterDef letter, GameData data) => data.HasFlag(letter.ReadFlag);

    /// <summary>True while a package letter's items are still waiting to be taken.</summary>
    public static bool HasUntakenItems(LetterDef letter, GameData data) =>
        letter.Items is { Count: > 0 }
        && letter.TakenFlag is not null
        && !data.HasFlag(letter.TakenFlag);

    /// <summary>Drives the mailbox's raised-flag signal: any delivered letter still unread.</summary>
    public static bool HasUnread(GameData data, GameTime now)
    {
        foreach (LetterDef letter in Delivered(data, now))
        {
            if (!IsRead(letter, data))
            {
                return true;
            }
        }
        return false;
    }
}
