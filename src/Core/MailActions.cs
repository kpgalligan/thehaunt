namespace TheHaunt.Core;

public enum MailOutcome
{
    Taken,          // the whole package landed in the inventory
    NoRoom,         // nothing moved — the model is bit-identical (harvest precedent)
    AlreadyTaken,   // the TakenFlag is stamped; the package was paid out before
    NothingToTake,  // an info letter (no Items/TakenFlag)
}

/// <summary>
/// The mail system's one inventory mutation: taking a letter's package. Pure and
/// story-flag-free like FarmActions — WorldSim.TakeLetterItems wraps this, stamps
/// the TakenFlag through SetStoryFlag on <see cref="MailOutcome.Taken"/>, and fires
/// the UI events; calling this outside the bus would pay the package twice.
/// </summary>
public static class MailActions
{
    public static MailOutcome TakeItems(LetterDef letter, GameData data)
    {
        if (letter.Items is not { Count: > 0 } || letter.TakenFlag is null)
        {
            return MailOutcome.NothingToTake;
        }
        if (data.HasFlag(letter.TakenFlag))
        {
            return MailOutcome.AlreadyTaken;
        }
        // All-or-nothing across the WHOLE package: two per-item HasRoomFor calls
        // would double-count the same empty slots, so the check simulates the real
        // adds against a scratch copy of the slot list.
        if (!RoomForAll(letter.Items, data.Player.Inventory))
        {
            return MailOutcome.NoRoom;
        }
        foreach (LetterItem item in letter.Items)
        {
            data.Player.Inventory.Add(item.ItemId, item.Count);
        }
        return MailOutcome.Taken;
    }

    private static bool RoomForAll(IReadOnlyList<LetterItem> items, InventoryData inventory)
    {
        var scratch = new List<ItemStackRecord?>(inventory.Slots.Count);
        foreach (ItemStackRecord? slot in inventory.Slots)
        {
            scratch.Add(slot is null ? null : new ItemStackRecord { ItemId = slot.ItemId, Count = slot.Count });
        }
        foreach (LetterItem item in items)
        {
            if (StackOps.Add(scratch, item.ItemId, item.Count) > 0)
            {
                return false;
            }
        }
        return true;
    }
}
