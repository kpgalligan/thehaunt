using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The town centre. Built today: the town hall and the general store (TownMap).
/// PLANNED (Kevin, 2026-08-26; kept as the map's spec 2026-09-01 / D7) — the centre
/// should be LARGE, and still needs:
///   - a medical clinic (Shelly's nursing years were spent there);
///   - the "Stumble Inn" — the nice bar in town, Billie's opposite number;
///   - about 10 homes for various characters spread around (where everybody lives
///     is deliberately unresolved);
///   - a town square and park area with benches, a fountain, and flowers around it.
/// PLANNED — beyond the centre: north of town leads to wilderness (fishing spots,
/// odd characters, cave/dungeon entrances go there eventually); south of town is
/// the power station (no details yet).
/// The planned queue is greppable: rg "PLANNED" src/Content.
/// </summary>
public static class Town
{
    public const string MapId = MapIds.Town;

    // [KEVIN] placeholder copy — the general store's board, and what its counter
    // answers outside hours. The hours restate ShopHours (Core); the store's NAME
    // is not invented.
    public const string StoreSignText = "General store. Open 9 to 5.";
    public const string CounterClosedLine = "Closed (9-5)";
}
