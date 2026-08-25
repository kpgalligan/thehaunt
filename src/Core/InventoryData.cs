using System.Text.Json.Serialization;

namespace TheHaunt.Core;

public sealed class InventoryData
{
    public const int Capacity = 10;                    // hotbar IS the inventory in v2

    public List<ItemStackRecord?> Slots { get; set; } = NewEmptySlots();  // null = empty; indices stable

    public int SelectedSlot { get; set; }              // 0..Capacity-1

    [JsonIgnore]
    public ItemStackRecord? Selected => SlotAt(SelectedSlot);

    public ItemStackRecord? SlotAt(int i) =>
        i >= 0 && i < Slots.Count ? Slots[i] : null;

    // Returns the overflow NOT placed; tops up same-id stacks lowest-index-first,
    // then fills empty slots.
    public int Add(string itemId, int count)
    {
        if (count <= 0)
        {
            return 0;
        }
        int maxStack = MaxStackFor(itemId);
        int remaining = count;
        for (int i = 0; i < Slots.Count && remaining > 0; i++)
        {
            var stack = Slots[i];
            if (stack is null || stack.ItemId != itemId || stack.Count >= maxStack)
            {
                continue;
            }
            int put = Math.Min(maxStack - stack.Count, remaining);
            stack.Count += put;
            remaining -= put;
        }
        for (int i = 0; i < Slots.Count && remaining > 0; i++)
        {
            if (Slots[i] is not null)
            {
                continue;
            }
            int put = Math.Min(maxStack, remaining);
            Slots[i] = new ItemStackRecord { ItemId = itemId, Count = put };
            remaining -= put;
        }
        return remaining;
    }

    // Returns the count actually removed, taking from lowest-index stacks first.
    public int Remove(string itemId, int count)
    {
        if (count <= 0)
        {
            return 0;
        }
        int remaining = count;
        for (int i = 0; i < Slots.Count && remaining > 0; i++)
        {
            var stack = Slots[i];
            if (stack is null || stack.ItemId != itemId)
            {
                continue;
            }
            int take = Math.Min(stack.Count, remaining);
            stack.Count -= take;
            remaining -= take;
            if (stack.Count <= 0)
            {
                Slots[i] = null;
            }
        }
        return count - remaining;
    }

    // Returns the count actually removed; nulls emptied stacks.
    public int RemoveFromSlot(int slot, int count)
    {
        var stack = SlotAt(slot);
        if (stack is null || count <= 0)
        {
            return 0;
        }
        int take = Math.Min(stack.Count, count);
        stack.Count -= take;
        if (stack.Count <= 0)
        {
            Slots[slot] = null;
        }
        return take;
    }

    // All-or-nothing consume from the selected stack.
    public bool TryConsumeSelected(int count)
    {
        var stack = Selected;
        if (stack is null || count <= 0 || stack.Count < count)
        {
            return false;
        }
        stack.Count -= count;
        if (stack.Count <= 0)
        {
            Slots[SelectedSlot] = null;
        }
        return true;
    }

    public int CountOf(string itemId)
    {
        int total = 0;
        foreach (var stack in Slots)
        {
            if (stack is not null && stack.ItemId == itemId)
            {
                total += stack.Count;
            }
        }
        return total;
    }

    public bool HasRoomFor(string itemId, int count)
    {
        if (count <= 0)
        {
            return true;
        }
        int maxStack = MaxStackFor(itemId);
        long room = 0;
        foreach (var stack in Slots)
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

    // Load repair. PADS Slots to Capacity but NEVER trims (raising Capacity later
    // must remain a constant change, not a migration); nulls degenerate entries;
    // KEEPS unknown ids and over-stacks intact (item deletion is data loss);
    // clamps SelectedSlot into range.
    public void Normalize()
    {
        Slots ??= new List<ItemStackRecord?>();
        while (Slots.Count < Capacity)
        {
            Slots.Add(null);
        }
        for (int i = 0; i < Slots.Count; i++)
        {
            var stack = Slots[i];
            if (stack is not null && (stack.Count <= 0 || string.IsNullOrEmpty(stack.ItemId)))
            {
                Slots[i] = null;
            }
        }
        // Selection stays within the visible hotbar even when an over-capacity save
        // keeps extra slots (those are preserved but not selectable).
        SelectedSlot = Math.Clamp(SelectedSlot, 0, Math.Min(Capacity, Slots.Count) - 1);
    }

    // Unknown ids get a conservative max stack of 1.
    private static int MaxStackFor(string itemId) => ItemDefs.TryGet(itemId)?.MaxStack ?? 1;

    private static List<ItemStackRecord?> NewEmptySlots()
    {
        var slots = new List<ItemStackRecord?>(Capacity);
        for (int i = 0; i < Capacity; i++)
        {
            slots.Add(null);
        }
        return slots;
    }
}
