using Godot;

namespace TheHaunt.World;

/// <summary>
/// Named atlas coordinates for the town terrain sheet (assets/sprites/town/terrain.png,
/// 16 columns x 4 rows of 16px tiles) plus the lookups that turn a surface map into
/// tiles. Coordinates and tile names come straight from the art handoff
/// (docs/designs/design_handoff_town_art/README.md §3).
/// </summary>
public static class TerrainTiles
{
    /// <summary>
    /// Dread escalation ships as a variant set of the SAME tiles at the SAME map
    /// coordinates, swapped by a story flag — no map is ever rebuilt (handoff
    /// §"Act escalation"). Every painted cell goes through <see cref="ForAct"/>, so
    /// adding Act II/III means filling in that one switch, not re-laying a map.
    /// </summary>
    public enum Act { One }

    /// <summary>
    /// Maps a base tile coordinate to its variant for <paramref name="act"/>. Act I is
    /// the identity map and the only set drawn today: no plum, bile-green or bone
    /// anywhere in the town exterior.
    /// </summary>
    public static Vector2I ForAct(Vector2I coords, Act act) => act switch
    {
        _ => coords,
    };

    // ---- Row 0: ground -------------------------------------------------
    public static readonly Vector2I[] Grass = { new(0, 0), new(1, 0), new(2, 0), new(3, 0) };
    public static readonly Vector2I GrassClover = new(4, 0);
    public static readonly Vector2I GrassStones = new(5, 0);
    public static readonly Vector2I GrassBare = new(6, 0);
    public static readonly Vector2I[] Dirt = { new(7, 0), new(8, 0), new(9, 0) };
    public static readonly Vector2I RutH = new(10, 0);
    public static readonly Vector2I RutV = new(11, 0);
    public static readonly Vector2I[] Gravel = { new(12, 0), new(13, 0) };

    // ---- Row 2: plaza --------------------------------------------------
    public static readonly Vector2I[] Cobble = { new(0, 2), new(1, 2) };
    public static readonly Vector2I CobbleWorn = new(2, 2);   // placed ONCE, at the plaza centre

    // ---- Boundary (all blocking) ---------------------------------------
    public static readonly Vector2I[] Woods =
    {
        new(14, 0), new(15, 0),               // woods_a, woods_b
        new(11, 2),                           // woods_c
        new(0, 3), new(1, 3), new(2, 3),      // woods_d, woods_e, woods_f
    };
    public static readonly Vector2I WoodsCornerSe = new(3, 3);
    public static readonly Vector2I WoodsCornerSw = new(4, 3);
    public static readonly Vector2I WoodsCornerNw = new(5, 3);
    public static readonly Vector2I WoodsCornerNe = new(6, 3);

    /// <summary>
    /// Transparent atlas cell registered by <see cref="TownTerrain"/> as a collision-only
    /// tile. The Obstacles layer paints it under building facades and props, which are
    /// drawn as sprites — the cell blocks and reports walkable=false without drawing.
    /// </summary>
    public static readonly Vector2I Blocker = new(15, 3);

    // ---- Row 1: dirt-over-grass, 16 configurations ---------------------
    // Indexed by a grass bitmask (N=1, E=2, S=4, W=8) in the handoff's column order:
    // 0 iso, 1 n, 2 e, 3 s, 4 w, 5 ne, 6 se, 7 sw, 8 nw, 9 centre, 10 ns, 11 ew,
    // 12 new, 13 sew, 14 nsw, 15 nse. Tiles are named by which sides RETAIN grass, so
    // the all-grass mask is column 0 and the no-grass mask is column 9.
    private static readonly int[] DirtColumnByGrassMask =
    {
        9,  // ....  no grass        -> dirt_c
        1,  // ...N                  -> dirt_n
        2,  // ..E.                  -> dirt_e
        5,  // ..EN                  -> dirt_ne
        3,  // .S..                  -> dirt_s
        10, // .S.N                  -> dirt_ns
        6,  // .SE.                  -> dirt_se
        15, // .SEN                  -> dirt_nse
        4,  // W...                  -> dirt_w
        8,  // W..N                  -> dirt_nw
        11, // W.E.                  -> dirt_ew
        12, // W.EN                  -> dirt_new
        7,  // WS..                  -> dirt_sw
        14, // WS.N                  -> dirt_nsw
        13, // WSE.                  -> dirt_sew
        0,  // WSEN grass all round  -> dirt_iso
    };

    /// <summary>Dirt-over-grass tile for a cell whose named sides still hold grass.</summary>
    public static Vector2I DirtEdge(bool grassN, bool grassE, bool grassS, bool grassW)
    {
        int mask = (grassN ? 1 : 0) | (grassE ? 2 : 0) | (grassS ? 4 : 0) | (grassW ? 8 : 0);
        return new Vector2I(DirtColumnByGrassMask[mask], 1);
    }

    /// <summary>
    /// Inner corners (row 2, cols 12-15): a cell with dirt on all four sides but grass
    /// in one diagonal, so the road nibbles around the grass nub. Only the single-diagonal
    /// case is representable; two or more grass diagonals fall back to plain dirt_c.
    /// </summary>
    public static Vector2I? DirtInnerCorner(bool grassNe, bool grassSe, bool grassSw, bool grassNw)
    {
        int count = (grassNe ? 1 : 0) + (grassSe ? 1 : 0) + (grassSw ? 1 : 0) + (grassNw ? 1 : 0);
        if (count != 1)
            return null;
        if (grassSe) return new Vector2I(12, 2);
        if (grassSw) return new Vector2I(13, 2);
        if (grassNw) return new Vector2I(14, 2);
        return new Vector2I(15, 2);
    }

    // ---- Row 2: cobble kerbs -------------------------------------------
    // kerb_n means "non-cobble to the north". Columns 3-10 in the handoff's order.
    private const int KerbN = 3, KerbE = 4, KerbS = 5, KerbW = 6;
    private const int KerbNe = 7, KerbSe = 8, KerbSw = 9, KerbNw = 10;

    /// <summary>
    /// Kerb tile for a cobble cell, by which sides are NOT cobble. Null = interior cell
    /// (the caller picks a cobble base). A plaza rectangle only ever produces the eight
    /// edge/corner cases; opposite pairs and three-sided cells fall back to the first
    /// matching single edge, which still reads correctly for a 1px kerb.
    /// </summary>
    public static Vector2I? Kerb(bool openN, bool openE, bool openS, bool openW)
    {
        int column = (openN, openE, openS, openW) switch
        {
            (false, false, false, false) => -1,
            (true, true, false, false) => KerbNe,
            (false, true, true, false) => KerbSe,
            (false, false, true, true) => KerbSw,
            (true, false, false, true) => KerbNw,
            (true, false, false, false) => KerbN,
            (false, true, false, false) => KerbE,
            (false, false, true, false) => KerbS,
            (false, false, false, true) => KerbW,
            _ => openN ? KerbN : openE ? KerbE : openS ? KerbS : KerbW,
        };
        return column < 0 ? null : new Vector2I(column, 2);
    }
}
