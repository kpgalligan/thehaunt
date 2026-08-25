namespace TheHaunt.Core;

// Shared stack algebra over nullable-slot lists, used by InventoryData and
// StorageData alike. Extracted verbatim from InventoryData (zero behavior
// change — the existing InventoryTests pin the refactor).
public static class StackOps
{
    // Unknown ids get a conservative max stack of 1.
    public static int MaxStackFor(string itemId) => ItemDefs.TryGet(itemId)?.MaxStack ?? 1;

    // Returns the overflow NOT placed; tops up same-id stacks lowest-index-first,
    // then fills empty slots.
    public static int Add(List<ItemStackRecord?> slots, string itemId, int count)
    {
        if (count <= 0)
        {
            return 0;
        }
        int maxStack = MaxStackFor(itemId);
        int remaining = count;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var stack = slots[i];
            if (stack is null || stack.ItemId != itemId || stack.Count >= maxStack)
            {
                continue;
            }
            int put = Math.Min(maxStack - stack.Count, remaining);
            stack.Count += put;
            remaining -= put;
        }
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i] is not null)
            {
                continue;
            }
            int put = Math.Min(maxStack, remaining);
            slots[i] = new ItemStackRecord { ItemId = itemId, Count = put };
            remaining -= put;
        }
        return remaining;
    }

    public static int CountOf(List<ItemStackRecord?> slots, string itemId)
    {
        int total = 0;
        foreach (var stack in slots)
        {
            if (stack is not null && stack.ItemId == itemId)
            {
                total += stack.Count;
            }
        }
        return total;
    }

    public static bool HasRoomFor(List<ItemStackRecord?> slots, string itemId, int count)
    {
        if (count <= 0)
        {
            return true;
        }
        int maxStack = MaxStackFor(itemId);
        long room = 0;
        foreach (var stack in slots)
        {
            if (stack is null)
            {
                room += maxStack;
            }
            else if (stack.ItemId == itemId && stack.Count < maxStack)
            {
                room += maxStack - stack.Count;
            }
            if (room >= count)
            {
                return true;
            }
        }
        return false;
    }
}
