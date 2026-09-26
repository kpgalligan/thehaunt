using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The farm, on the town's north outskirts (TestMap is its view;
/// data/maps/farm.tmx its Tiled map).
/// CANON (Kevin, 2026-08-26): it has seen better days. The land is certainly
/// fertile, but the barn is run down and empty (Places/BarnRules) and the house is
/// livable enough, but sparse. A bin takes crops overnight in return for cash. The
/// previous owner's farewell letter (LetterDefs) points Jane to the starter tools
/// and seeds waiting in the barn chest (StarterKit — fetching them is the first
/// errand). Jane upgrades the farm over time to make real money.
/// THE WINE — canon plot hook, deliberately unresolved: the wine the old farmer
/// served at the sale "seems to have not been made on his farm". It points at the
/// supernatural-wine specialty Kevin is weighing (docs/design.md, open questions);
/// do not resolve or reference it in dialogue without him.
/// Dropped from canon (Kevin, 2026-09-01 / D5): the well — nothing needs one until
/// a water mechanic exists.
/// </summary>
public static class Farm
{
    public const string MapId = MapIds.Farm;

    // [KEVIN] placeholder copy. The map (data/maps/farm.tmx) holds the sign
    // PLACEMENTS; the words live here and are resolved by placement id at build time
    // (the promotion PlacementFields.Text always promised).
    public const string YardSignText = "Placeholder sign. Real text comes later.";
    public const string BlockadeSignText = "The storm brought half the hillside down. No getting through today.";

    /// <summary>Copy for the farm's recipe-driven signs, by placement id. Null = an
    /// unpromoted sign (an editor-added board still carrying its own recipe text).</summary>
    public static string? SignTextFor(string placementId) => placementId switch
    {
        "Sign" => YardSignText,
        "BlockadeSign" => BlockadeSignText,
        _ => null,
    };
}
