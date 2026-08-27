using Godot;

namespace TheHaunt.World;

/// <summary>
/// Source rects for the furniture sheet (assets/sprites/interior/furniture.png), verbatim
/// from the farm/interiors handoff §5 — all 36 of them, though the handoff's header
/// miscounts them as 34 (its own list enumerates 36, and the sheet draws 36).
///
/// Every piece is drawn to STAND ON its anchor cell: a 16x32 upright occupies one floor
/// tile and overhangs one tile upward, exactly like the exterior facades, so a piece
/// pairs with <see cref="Prop.Anchor"/> and its collision comes from a blocker on the
/// Obstacles layer.
///
/// <see cref="ChairFront"/>/<see cref="ChairBack"/>/<see cref="ChairSide"/> are three views
/// of one chair: put the back view above a table and the side view beside it and the
/// seating reads correctly.
/// </summary>
public static class Furniture
{
    public const string TexturePath = "res://assets/sprites/interior/furniture.png";

    // ---- 16x32 uprights (one tile of floor, one tile of overhang) ------
    public static readonly Rect2 Bed = new(0, 0, 16, 32);
    public static readonly Rect2 Stove = new(16, 0, 16, 32);
    public static readonly Rect2 Cupboard = new(32, 0, 16, 32);
    public static readonly Rect2 Ladder = new(48, 0, 16, 32);
    public static readonly Rect2 Stall = new(64, 0, 16, 32);
    public static readonly Rect2 Candles = new(80, 0, 16, 32);
    public static readonly Rect2 Dresser = new(96, 0, 16, 32);
    public static readonly Rect2 Banner = new(112, 0, 16, 32);
    public static readonly Rect2 TallShelf = new(128, 0, 16, 32);
    public static readonly Rect2 Stained = new(144, 0, 16, 32);

    // ---- 16x16 smalls --------------------------------------------------
    public static readonly Rect2 Cradle = new(160, 0, 16, 16);
    public static readonly Rect2 Lectern = new(176, 0, 16, 16);
    public static readonly Rect2 Till = new(192, 0, 16, 16);
    public static readonly Rect2 ChairFront = new(208, 0, 16, 16);
    public static readonly Rect2 ChairBack = new(224, 0, 16, 16);
    public static readonly Rect2 ChairSide = new(240, 0, 16, 16);

    public static readonly Rect2 Stool = new(160, 16, 16, 16);
    public static readonly Rect2 Pot = new(176, 16, 16, 16);
    public static readonly Rect2 Sack = new(192, 16, 16, 16);
    public static readonly Rect2 Bucket = new(208, 16, 16, 16);
    public static readonly Rect2 Lamp = new(224, 16, 16, 16);
    public static readonly Rect2 Books = new(240, 16, 16, 16);

    // ---- 32x16 surfaces (two tiles wide) -------------------------------
    public static readonly Rect2 Table = new(0, 32, 32, 16);
    public static readonly Rect2 Bench = new(32, 32, 32, 16);
    public static readonly Rect2 Desk = new(64, 32, 32, 16);
    public static readonly Rect2 Workbench = new(96, 32, 32, 16);
    public static readonly Rect2 ToolRack = new(128, 32, 32, 16);
    public static readonly Rect2 SeedBins = new(160, 32, 32, 16);
    public static readonly Rect2 Cart = new(192, 32, 32, 16);
    public static readonly Rect2 Altar = new(224, 32, 32, 16);

    // ---- larger --------------------------------------------------------
    public static readonly Rect2 Pew = new(0, 48, 48, 16);
    public static readonly Rect2 LongTable = new(48, 48, 48, 32);
    public static readonly Rect2 Haystack = new(96, 48, 32, 32);
    public static readonly Rect2 Crates = new(128, 48, 32, 32);
    public static readonly Rect2 WideShelf = new(160, 48, 48, 16);
    public static readonly Rect2 Loom = new(208, 48, 32, 32);

    /// <summary>Footprint width in tiles — how many cells a piece's base row covers.</summary>
    public static int Tiles(Rect2 source) => Mathf.RoundToInt(source.Size.X) / MapRoot.TileSize;
}
