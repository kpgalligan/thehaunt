using Godot;
using TheHaunt.World;

namespace TheHaunt.EditorTools;

/// <summary>
/// How much ground a placement covers, in TILES — what the mapper outlines in the
/// viewport, and what a click has to land inside to grab the thing.
///
/// A recipe deliberately does not store a footprint. A tree is three tiles wide because
/// its art is 48 pixels wide, and a <c>w</c> field sitting next to a 48px sprite is two
/// truths waiting to diverge — the same rule that keeps the shipping bin's width in
/// <see cref="FarmBuildings.BinArt"/> rather than in the file. So this asks the tables the
/// map's own builder asks, and derives the span from the art. Where a KIND owns its span
/// instead of its art (an exit's trigger volume, a counter's strip) the recipe's w/h
/// fields ARE the answer, because there is no sprite for them to disagree with.
///
/// Only the farm's ids resolve today, because the farm is the only map that has been
/// seeded. Everything else answers one tile: exactly right for a spawn marker or a door,
/// and merely conservative for anything else — a too-small footprint costs a click near
/// the edge of a sprite, a too-large one steals clicks from its neighbours. Adding the
/// town is a case in <see cref="Of"/> pointing at TownProps.
///
/// Editor-only, and pure: no Godot editor API, no node, nothing that needs the editor to
/// be running. That is what lets the headless suite cover it.
/// </summary>
public static class PlacementFootprint
{
    /// <summary>The cells a placement covers, anchored the way its kind's builder anchors it.</summary>
    public static Rect2I Of(MapPlacement placement)
    {
        switch (placement.Kind)
        {
            case PlacementKinds.Prop:
                // Drawn in elevation: the cell is the LEFT COLUMN of the BASE ROW and the
                // sprite rises off it (Prop.Anchor), so the rect grows UP, never down.
                return Elevation(placement, ArtSize(placement.Kind, placement.Id));

            case PlacementKinds.ShippingBin:
                // Flat and two tiles wide; the cell is its top-left, which is how the
                // bin's own reservation in TestMap.BuildInteractables reads it.
                return Flat(placement, ArtSize(placement.Kind, placement.Id));

            case PlacementKinds.Exit:
            case PlacementKinds.ShopCounter:
                return new Rect2I(placement.Cell, new Vector2I(
                    Mathf.Max(1, placement.Int(PlacementFields.Width, 1)),
                    Mathf.Max(1, placement.Int(PlacementFields.Height, 1))));

            default:
                return new Rect2I(placement.Cell, Vector2I.One);
        }
    }

    /// <summary>Cells covered. Ranks overlapping hits — the tighter footprint wins a shared cell.</summary>
    public static int Area(MapPlacement placement)
    {
        Vector2I size = Of(placement).Size;
        return size.X * size.Y;
    }

    // Pixel size of the art an id names, or one tile when this build cannot resolve it.
    // Swallowing the ArgumentException is deliberate and is NOT what the map does: an
    // unknown id has to fail loudly at BUILD time, and TestMap.LoadPlacements does exactly
    // that. Here it would only mean refusing to outline a record whose problem the dock is
    // already reporting in words.
    private static Vector2 ArtSize(string kind, string id)
    {
        try
        {
            return kind switch
            {
                PlacementKinds.Prop => FarmBuildings.TreeArt(id).Size,
                PlacementKinds.ShippingBin => FarmBuildings.BinArt(id).Closed.Size,
                _ => TileSquare,
            };
        }
        catch (ArgumentException)
        {
            return TileSquare;
        }
    }

    private static Rect2I Elevation(MapPlacement placement, Vector2 pixels)
    {
        Vector2I size = InTiles(pixels);
        return new Rect2I(placement.X, placement.Y - size.Y + 1, size.X, size.Y);
    }

    private static Rect2I Flat(MapPlacement placement, Vector2 pixels) =>
        new(placement.Cell, InTiles(pixels));

    // Ceil, not round: a sprite that spills three pixels into the next column still covers
    // it as far as a click is concerned.
    private static Vector2I InTiles(Vector2 pixels) => new(
        Mathf.Max(1, Mathf.CeilToInt(pixels.X / MapRoot.TileSize)),
        Mathf.Max(1, Mathf.CeilToInt(pixels.Y / MapRoot.TileSize)));

    private static readonly Vector2 TileSquare = new(MapRoot.TileSize, MapRoot.TileSize);
}
