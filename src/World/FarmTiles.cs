using Godot;

namespace TheHaunt.World;

/// <summary>
/// Named atlas coordinates for the farm terrain sheet (assets/sprites/farm/farm_terrain.png,
/// 16 columns x 4 rows of 16px tiles), from the farm art handoff
/// (docs/designs/design_handoff_farm_interiors/README.md §1).
///
/// Row 0 holds the soil bases, the furrow pair and the field dressing; rows 1 and 2 are
/// the dry and wet soil-over-grass autotile sets, laid out in the SAME column order as
/// the town's dirt set, so both go through <see cref="TerrainTiles.EdgeColumn"/>; row 3
/// is fences, gates, paths and ground detail.
/// </summary>
public static class FarmTiles
{
    /// <summary>
    /// Same seam as <see cref="TerrainTiles.ForAct"/>: dread escalation swaps a variant
    /// set of the SAME tiles at the SAME coordinates, so no map is ever re-laid.
    /// </summary>
    public enum Act { One }

    public static Vector2I ForAct(Vector2I coords, Act act) => act switch
    {
        _ => coords,
    };

    // ---- Row 0: soil bases, furrows, field dressing --------------------
    public static readonly Vector2I[] SoilDry = { new(0, 0), new(1, 0) };
    public static readonly Vector2I[] SoilWet = { new(2, 0), new(3, 0) };
    public static readonly Vector2I FurrowDryH = new(4, 0);
    public static readonly Vector2I FurrowDryV = new(5, 0);
    public static readonly Vector2I FurrowWetH = new(6, 0);
    public static readonly Vector2I FurrowWetV = new(7, 0);

    /// <summary>
    /// The farm's base ground: rougher and darker than the town's grass on purpose, so
    /// the two maps never read as the same field (handoff §1).
    /// </summary>
    public static readonly Vector2I[] Pasture = { new(8, 0), new(9, 0), new(11, 3) };

    public static readonly Vector2I[] Weeds = { new(10, 0), new(11, 0) };
    public static readonly Vector2I RockSmall = new(12, 0);
    public static readonly Vector2I RockLarge = new(13, 0);   // solid
    public static readonly Vector2I Stump = new(14, 0);       // solid
    public static readonly Vector2I Puddle = new(15, 0);

    // ---- Rows 1-2: soil-over-grass autotile ----------------------------
    private const int DryRow = 1;
    private const int WetRow = 2;

    /// <summary>
    /// Soil tile for a worked cell, named by which sides are still grass. Column 9
    /// ("c", no grass on any side) is what a plot interior uses.
    /// </summary>
    public static Vector2I SoilEdge(bool wet, bool grassN, bool grassE, bool grassS, bool grassW) =>
        new(TerrainTiles.EdgeColumn(grassN, grassE, grassS, grassW), wet ? WetRow : DryRow);

    /// <summary>
    /// Furrow tile for a cell whose worked neighbours give it a direction to be hoed in
    /// — a plot worked in rows reads as deliberate rather than as a rectangle of mud.
    /// Derived at paint time, so no save field and nothing to migrate (handoff §1, the
    /// "derived" option).
    ///
    /// East-west wins ties, which is the whole of why <c>furrow_*_v</c> goes unused
    /// today: only a fully-surrounded cell gets a furrow at all, and such a cell always
    /// has soil east AND west. The vertical pair waits for a painter that knows the
    /// plot's shape rather than just this cell's neighbours — the same shelf
    /// <c>rut_h</c>/<c>rut_v</c> sit on in the town.
    /// </summary>
    public static Vector2I? Furrow(bool wet, bool soilN, bool soilE, bool soilS, bool soilW)
    {
        if (soilE && soilW)
            return wet ? FurrowWetH : FurrowDryH;
        if (soilN && soilS)
            return wet ? FurrowWetV : FurrowDryV;
        return null;   // a lone cell or an elbow: no direction to read
    }

    // ---- Row 3: fences, gates, paths, detail ---------------------------
    public static readonly Vector2I FenceH = new(0, 3);        // solid
    public static readonly Vector2I FenceV = new(1, 3);        // solid
    public static readonly Vector2I FencePost = new(2, 3);     // solid
    public static readonly Vector2I FenceCornerSe = new(3, 3); // solid
    public static readonly Vector2I FenceCornerSw = new(4, 3); // solid
    public static readonly Vector2I FenceCornerNw = new(5, 3); // solid
    public static readonly Vector2I FenceCornerNe = new(6, 3); // solid
    public static readonly Vector2I GateClosed = new(7, 3);    // solid
    public static readonly Vector2I GateOpen = new(8, 3);
    public static readonly Vector2I[] Path = { new(9, 3), new(10, 3) };
    public static readonly Vector2I DeadGrass = new(12, 3);
    public static readonly Vector2I Flowers = new(13, 3);
    public static readonly Vector2I Log = new(14, 3);          // solid
    public static readonly Vector2I HayScatter = new(15, 3);

    // ---- Placement names -----------------------------------------------

    /// <summary>
    /// The ids a recipe's <see cref="PlacementKinds.Scatter"/> records carry, resolved
    /// back to coordinates HERE so a name lives beside the tile it names and the two
    /// cannot drift. A placement stores the NAME, never the atlas coordinate, which is
    /// what keeps <see cref="ForAct"/> in the path when the dread escalation swaps the
    /// variant set underneath it.
    ///
    /// Only the single-variant dressing tiles are nameable. Pasture, weeds and path are
    /// picked per cell by the scatter hash, and a name meaning "one of three, depending
    /// on where you put it" would paint a different tile every time the editor nudged it
    /// one cell — you would drag a rock and watch it become a puddle.
    /// </summary>
    public const string RockSmallId = "rock_small";
    public const string RockLargeId = "rock_large";
    public const string StumpId = "stump";
    public const string PuddleId = "puddle";
    public const string DeadGrassId = "dead_grass";
    public const string FlowersId = "flowers";
    public const string LogId = "log";
    public const string HayId = "hay";

    // One table rather than a switch plus a list of legal names: the palette an editor
    // offers and the lookup a build resolves through have to be the same set, and two
    // collections of the same strings is a drift waiting to happen.
    private static readonly Dictionary<string, Vector2I> ScatterTiles = new(StringComparer.Ordinal)
    {
        [RockSmallId] = RockSmall,
        [RockLargeId] = RockLarge,
        [StumpId] = Stump,
        [PuddleId] = Puddle,
        [DeadGrassId] = DeadGrass,
        [FlowersId] = Flowers,
        [LogId] = Log,
        [HayId] = HayScatter,
    };

    /// <summary>Every scatter id this sheet answers to — an editor's palette, in one place.</summary>
    public static IReadOnlyCollection<string> ScatterIds => ScatterTiles.Keys;

    /// <summary>
    /// The tile a scatter placement's id names. Throws on anything else, deliberately: an
    /// unknown KIND is a newer branch's work and rides through a load/save untouched, but
    /// an unknown ID inside a kind this build DOES build is a typo in a file someone is
    /// editing right now, and quietly painting nothing is the worst possible answer to it.
    /// </summary>
    public static Vector2I ByName(string id) =>
        ScatterTiles.TryGetValue(id, out Vector2I tile)
            ? tile
            : throw new ArgumentException(
                $"'{id}' is not a farm scatter tile. Known: {string.Join(", ", ScatterTiles.Keys)}.",
                nameof(id));
}
