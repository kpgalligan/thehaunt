using Godot;

namespace TheHaunt.World;

/// <summary>
/// Source rects for the farm's drawn structures and trees
/// (assets/sprites/farm/farm_buildings.png), from the farm/interiors handoff §2. Every
/// rect is drawn base-down and pairs with a bottom-centre <see cref="Prop.Anchor"/>.
///
/// The farmhouse's bottom four rows are the footprint and its top two overhang; a tree's
/// bottom row is its trunk cell and everything above it is canopy, so only that one cell
/// is ever solid — the player walks under the branches.
/// </summary>
public static class FarmBuildings
{
    public const string TexturePath = "res://assets/sprites/farm/farm_buildings.png";

    public static readonly Rect2 Farmhouse = new(0, 0, 96, 96);
    public const int FarmhouseTiles = 6;
    public const int FarmhouseFootprintRows = 4;

    /// <summary>Tile column (0-5) the drawn front door falls in.</summary>
    public const int FarmhouseDoorColumn = 3;

    public static readonly Rect2 TreeLeafy = new(96, 0, 48, 64);
    public static readonly Rect2 TreeBare = new(144, 0, 48, 64);
    public const int TreeTiles = 3;

    /// <summary>Tile column (0-2) of the trunk — the only solid cell of a tree.</summary>
    public const int TreeTrunkColumn = 1;

    /// <summary>Rows of empty pixels below a tree's shadow, inside its source rect.</summary>
    public const float TreeInkGap = 2f;

    public static readonly Rect2 BinClosed = new(192, 0, 32, 16);
    public static readonly Rect2 BinOpen = new(224, 0, 32, 16);

    // ---- Placement names -----------------------------------------------
    // The ids a recipe carries for the pieces of this sheet that are placed rather than
    // structural. The farmhouse and the barn are deliberately absent: their columns feed
    // the surface fill, the footprint blockers and the door cells, so they are geometry
    // the map derives, not a thing anyone drags.

    public const string TreeLeafyId = "tree_leafy";
    public const string TreeBareId = "tree_bare";
    public const string BinId = "bin";

    /// <summary>
    /// Source rect for a <see cref="PlacementKinds.Prop"/> id. Both trees share
    /// <see cref="TreeTiles"/>, <see cref="TreeTrunkColumn"/> and
    /// <see cref="TreeInkGap"/>, which is why the id resolves to a rect alone — and why
    /// anything that is NOT a tree has to throw rather than borrow a trunk column that
    /// would put its blocker in the wrong cell.
    /// </summary>
    public static Rect2 TreeArt(string id) => id switch
    {
        TreeLeafyId => TreeLeafy,
        TreeBareId => TreeBare,
        _ => throw new ArgumentException(
            $"'{id}' is not a farm tree. Known: {TreeLeafyId}, {TreeBareId}.", nameof(id)),
    };

    /// <summary>
    /// The shut/open pair a <see cref="PlacementKinds.ShippingBin"/> id names. One entry
    /// today and still a lookup, because the alternative is an id nothing validates: a
    /// typo would build a bin anyway and the file would be quietly meaningless.
    /// </summary>
    public static (Rect2 Closed, Rect2 Open) BinArt(string id) => id == BinId
        ? (BinClosed, BinOpen)
        : throw new ArgumentException($"'{id}' is not a shipping bin. Known: {BinId}.", nameof(id));
}
