using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The one-shot exporter that turns a map's C# placement literals into its recipe file.
///
/// Every map in this game was hand-written before it was data, so every map has to be
/// SEEDED once: read the placements off the code that already holds them and write the
/// canonical JSON. Doing it this way rather than transcribing coordinates is not laziness
/// — it is the only version of the job whose fidelity is guaranteed by construction. A
/// typo in a hand-copied boulder is a boulder that moved, and nothing would catch it but
/// a screenshot taken by someone who happened to look at that corner of the map.
///
/// It stays useful after the farm. The town's plaza props and lamp posts and each
/// interior's furniture are the same problem, and each one arrives here as a case in
/// <see cref="For"/> pointing at that map's own DefaultRecipe — the defaults stay beside
/// the arrays they are built from, where they cannot drift from them.
///
/// The seed is not a migration and never runs by itself. Once a map is seeded, its file
/// is the map; the seed lives on as the fallback for a missing file and as the thing the
/// drift guard (MapSeedTests) holds the shipped file against. The moment someone drags
/// something in the editor, the two part company on purpose and the guard says so.
/// </summary>
public static class MapRecipeSeeds
{
    /// <summary>Whether this map has code defaults to seed from — false for one already born as data.</summary>
    public static bool Has(string mapId) => mapId == MapIds.Farm;

    /// <summary>
    /// The recipe a map's code defaults describe. Throws rather than returning an empty
    /// recipe for an unseeded map: an empty one written to disk would be a file that
    /// erases every placement the map still builds in code.
    /// </summary>
    public static MapRecipe For(string mapId) => mapId switch
    {
        MapIds.Farm => TestMap.DefaultRecipe(),
        _ => throw new ArgumentException(
            $"No seed for map '{mapId}' — its placements are still C# literals, " +
            "or it never had any. Add a case here pointing at its DefaultRecipe.",
            nameof(mapId)),
    };

    /// <summary>
    /// Writes the seed to the map's own path and returns it. EDITOR-ONLY, like every
    /// other recipe write: res:// is inside the .pck in an exported game, and a running
    /// game must never author content.
    ///
    /// Overwrites without asking. That is safe exactly once per map — before anyone has
    /// edited the file — and destructive every time after, which is what the drift guard
    /// is there to make impossible to do by accident.
    /// </summary>
    public static string Export(string mapId)
    {
        string path = MapRecipeFile.PathFor(mapId);
        MapRecipeFile.WriteTo(For(mapId), path);
        return path;
    }
}
