namespace TheHaunt.World;

/// <summary>
/// The kinds a <see cref="MapPlacement"/> can be, and what each one's Id names. The only
/// legal kind strings in code — the StoryKeys precedent: a kind typed as a literal at a
/// call site is a kind nothing validates.
///
/// A recipe file may hold kinds that are NOT listed here (a newer branch's work opened in
/// an older build); those survive load and save untouched. <see cref="Contains"/> is
/// therefore "does this build BUILD it", never "is this legal in a file".
///
/// Adding a kind is additive by construction: a constant here, a case in the map builder
/// that consumes it, and nothing in the format changes. There is deliberately no C# type
/// per kind — the per-kind extras live in scalar fields (<see cref="PlacementFields"/>),
/// so a new kind costs no new parsing and no new writer branch.
/// </summary>
public static class PlacementKinds
{
    /// <summary>Base-anchored sprite in elevation: trees, facades, plaza dressing. Id names an art entry.</summary>
    public const string Prop = "prop";

    /// <summary>One painted ground/dressing CELL: boulders, stumps, logs. Id names a tile, so it still goes through ForAct.</summary>
    public const string Scatter = "scatter";

    /// <summary>An interior piece off the furniture sheet plus its blocker. Id names a Furniture entry; "blocks" opts out.</summary>
    public const string Furniture = "furniture";

    /// <summary>A Marker2D under "Spawns". Id is the spawn NAME travel asks for ("default", "entry", "road").</summary>
    public const string Spawn = "spawn";

    /// <summary>A Door. Id is the target map id; "spawn" is the target spawn.</summary>
    public const string Door = "door";

    /// <summary>A walk-on MapExit. Id is the target map id; "spawn" the target spawn, "w"/"h" the trigger's tile span.</summary>
    public const string Exit = "exit";

    /// <summary>A readable Sign. Id names the sign; "text" carries the copy until it moves to a table of its own.</summary>
    public const string Sign = "sign";

    /// <summary>The bed. Id names its art entry.</summary>
    public const string Bed = "bed";

    /// <summary>A storage chest. Id is the STORAGE id (StorageIds), because that is what its contents live under.</summary>
    public const string Chest = "chest";

    /// <summary>The shipping bin. Id names its art entry.</summary>
    public const string ShippingBin = "shipping_bin";

    /// <summary>The shop interaction strip. Id is the catalog id; "w"/"h" the strip's tile span.</summary>
    public const string ShopCounter = "shop_counter";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Prop, Scatter, Furniture, Spawn, Door, Exit, Sign, Bed, Chest, ShippingBin, ShopCounter,
    };

    /// <summary>True when THIS build knows how to build the kind — not whether a file may hold it.</summary>
    public static bool Contains(string kind) => All.Contains(kind);
}

