namespace TheHaunt.Core;

// One named container's slots (chest etc). Lives in GameData.Storages keyed by
// storage id; lazy-created on first open via GameData.GetStorage.
public sealed class StorageData
{
    public List<ItemStackRecord?> Slots { get; set; } = new();   // null = empty; indices stable

    // Returns the overflow NOT placed (see StackOps.Add).
    public int Add(string itemId, int count) => StackOps.Add(Slots, itemId, count);

    // Load repair. Pads Slots to capacity when non-null (known storage ids only —
    // unknown keys pass null and round-trip un-padded; their capacity is not ours
    // to invent) but NEVER trims over-capacity saves (raising a capacity later must
    // remain a constant change, not a migration); nulls degenerate entries; KEEPS
    // unknown item ids and over-stacks verbatim (item deletion is data loss).
    public void Normalize(int? capacity)
    {
        Slots ??= new List<ItemStackRecord?>();
        if (capacity is int cap)
        {
            while (Slots.Count < cap)
            {
                Slots.Add(null);
            }
        }
        for (int i = 0; i < Slots.Count; i++)
        {
            var stack = Slots[i];
            if (stack is not null && (stack.Count <= 0 || string.IsNullOrEmpty(stack.ItemId)))
            {
                Slots[i] = null;
            }
        }
    }
}
