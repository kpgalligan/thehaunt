namespace TheHaunt.Core;

public readonly record struct ShopEntry(string ItemId, int BuyPrice);

public enum BuyResult { Ok, InsufficientFunds, NoRoom, UnknownItem }

// Per-shop buy catalogs. BuyPrice lives here, NOT on ItemDef, so per-shop
// pricing stays possible. NO counter selling in 3b — the shipping bin stays
// the sole sell path. A validation test pins every catalog id resolving in
// ItemDefs with BuyPrice > 0.
public static class ShopCatalog
{
    public const string GeneralStore = "general_store";   // catalog id == store map id

    // Insertion order below is the canonical row order for shop UIs.
    public static IReadOnlyDictionary<string, IReadOnlyList<ShopEntry>> All { get; } = Build();

    // Null-tolerant lookup for catalog ids coming from the open-shop session.
    public static IReadOnlyList<ShopEntry>? TryGet(string catalogId)
        => All.TryGetValue(catalogId, out var entries) ? entries : null;

    private static Dictionary<string, IReadOnlyList<ShopEntry>> Build()
    {
        // Buy prices are 2x sell; seeds-only catalog for now [KEVIN].
        return new Dictionary<string, IReadOnlyList<ShopEntry>>
        {
            [GeneralStore] = new ShopEntry[]
            {
                new("turnip_seeds", 20),
                new("greenbean_seeds", 30),
            },
        };
    }
}
