#if TOOLS
using Godot;
using TheHaunt.Core;
using TheHaunt.EditorTools;
using TheHaunt.World;

namespace TheHaunt.Addons.HauntMapper;

/// <summary>
/// Draws the mapper's overlays over the 2D viewport. Everything here is geometry the game
/// deliberately never shows: the grid a placement snaps to, the cells the hoe refuses,
/// the transparent blockers doing the colliding, the tiles NPCs teleport onto, and the
/// footprint of every record in the recipe.
///
/// It draws into the EDITOR's viewport control, not into the map — the map subtree sits
/// at ProcessMode.Disabled between scrubs (MapStage's lighting cost note), so a gizmo
/// node under it would be dead most of the time and would also serialise into whatever
/// scene someone saved next. A draw pass owned by the plugin has neither problem.
///
/// The transform is the caller's: stage-local pixels -> viewport-control pixels. Every
/// coordinate below starts as a TILE, is multiplied up to stage pixels, and only then
/// goes through it, so nothing here ever has to know about the editor's zoom or pan.
/// </summary>
internal static class MapperOverlay
{
    // Cool, low-contrast: the grid is a reference, not a subject. Anything louder turns
    // a 40x30 map into graph paper with a farm somewhere behind it.
    private static readonly Color GridColor = new(1f, 1f, 1f, 0.09f);
    private static readonly Color GridEdgeColor = new(1f, 1f, 1f, 0.22f);

    // Red for "solid", the same read as a collision shape anywhere else in the editor.
    private static readonly Color BlockerColor = new(0.95f, 0.25f, 0.2f, 0.20f);

    // Cyan hatch for "you may not till here" — a different hue AND a different mark from
    // the blockers, because the two overlap constantly and a shared colour would make the
    // interesting case (reserved but NOT solid: roof overhangs, doorways) unreadable.
    private static readonly Color ReservedColor = new(0.3f, 0.85f, 0.95f, 0.45f);

    // Amber, as the brief asks, and the warmest thing on screen: an NPC slot is the one
    // overlay that marks where something ARRIVES rather than where something is.
    private static readonly Color NpcColor = new(1f, 0.68f, 0.15f, 0.75f);

    private static readonly Color PlacementColor = new(0.55f, 1f, 0.6f, 0.55f);
    private static readonly Color AnchorColor = new(0.55f, 1f, 0.6f, 0.85f);
    private static readonly Color SelectedColor = new(1f, 0.95f, 0.35f, 1f);
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.35f);
    private static readonly Color LabelColor = new(1f, 1f, 1f, 0.9f);

    // Below this many screen pixels per tile the grid is a grey wash and the labels are
    // a smear, so both switch off rather than making the map harder to look at.
    private const float MinGridTilePixels = 5f;
    private const float MinLabelTilePixels = 18f;

    public static void Draw(
        Control canvas, Transform2D toCanvas, MapStage stage,
        OverlayLayers layers, int selected, Vector2I? hover)
    {
        MapRoot? map = stage.Map;
        if (map == null)
        {
            return;   // the build failed; the dock is showing why
        }

        float tilePixels = toCanvas.Scale.X * MapRoot.TileSize;
        Rect2I bounds = map.Ground?.GetUsedRect() ?? new Rect2I();

        if (layers.HasFlag(OverlayLayers.Grid) && tilePixels >= MinGridTilePixels)
        {
            DrawGrid(canvas, toCanvas, bounds);
        }
        if (layers.HasFlag(OverlayLayers.Blockers))
        {
            DrawBlockers(canvas, toCanvas, map);
        }
        if (layers.HasFlag(OverlayLayers.Reserved))
        {
            DrawReserved(canvas, toCanvas, map);
        }
        if (layers.HasFlag(OverlayLayers.NpcSlots))
        {
            DrawNpcSlots(canvas, toCanvas, map.MapId, tilePixels);
        }
        if (layers.HasFlag(OverlayLayers.Placements))
        {
            DrawPlacements(canvas, toCanvas, stage, selected, tilePixels);
        }
        if (hover is { } cell)
        {
            canvas.DrawRect(Screen(toCanvas, new Rect2I(cell, Vector2I.One)), HoverColor, filled: false, width: 1f);
        }
    }

    // ------------------------------------------------------------------
    // The layers
    // ------------------------------------------------------------------

    private static void DrawGrid(Control canvas, Transform2D toCanvas, Rect2I bounds)
    {
        if (bounds.Size.X <= 0 || bounds.Size.Y <= 0)
        {
            return;
        }
        int top = bounds.Position.Y, bottom = bounds.End.Y;
        int left = bounds.Position.X, right = bounds.End.X;

        for (int x = left; x <= right; x++)
        {
            // The map's own edges are drawn harder: on a map whose border is two rows of
            // woods, the outline is the only thing that says where the map stops.
            Color color = x == left || x == right ? GridEdgeColor : GridColor;
            canvas.DrawLine(
                toCanvas * Pixels(x, top), toCanvas * Pixels(x, bottom), color, 1f);
        }
        for (int y = top; y <= bottom; y++)
        {
            Color color = y == top || y == bottom ? GridEdgeColor : GridColor;
            canvas.DrawLine(
                toCanvas * Pixels(left, y), toCanvas * Pixels(right, y), color, 1f);
        }
    }

    private static void DrawBlockers(Control canvas, Transform2D toCanvas, MapRoot map)
    {
        // Read off the layer by name, the way MapRoot.IsStandable does — the Obstacles
        // layer is a naming convention shared by every map in the game, not a field.
        var obstacles = map.GetNodeOrNull<TileMapLayer>("Obstacles");
        if (obstacles == null)
        {
            return;
        }
        foreach (Vector2I cell in obstacles.GetUsedCells())
        {
            canvas.DrawRect(Screen(toCanvas, new Rect2I(cell, Vector2I.One)), BlockerColor);
        }
    }

    private static void DrawReserved(Control canvas, Transform2D toCanvas, MapRoot map)
    {
        foreach (Vector2I cell in map.ReservedTiles())
        {
            // Two diagonals rather than a fill: reserved cells sit on top of blockers
            // constantly, and a hatch reads through a fill where a second fill would just
            // become a third colour nobody can name.
            Rect2 rect = Screen(toCanvas, new Rect2I(cell, Vector2I.One));
            canvas.DrawLine(rect.Position, rect.End, ReservedColor, 1f);
            canvas.DrawLine(
                new Vector2(rect.Position.X, rect.End.Y),
                new Vector2(rect.End.X, rect.Position.Y), ReservedColor, 1f);
        }
    }

    private static void DrawNpcSlots(
        Control canvas, Transform2D toCanvas, string mapId, float tilePixels)
    {
        Font font = canvas.GetThemeDefaultFont();
        foreach (NpcDef def in NpcDefs.All.Values)
        {
            // EVERY entry for this map, not the one the stage's clock resolves to: the
            // question an editor has to answer is "can anything ever stand here", because
            // that is what makes a tile a bad place to drop a boulder. The one currently
            // staged is already visible — it is a drawn NpcView in the preview.
            foreach (ScheduleEntry entry in def.Schedule)
            {
                if (entry.Placement.MapId != mapId)
                {
                    continue;
                }
                var cell = new Vector2I(entry.Placement.TileX, entry.Placement.TileY);
                Rect2 rect = Screen(toCanvas, new Rect2I(cell, Vector2I.One));
                canvas.DrawRect(rect, NpcColor with { A = 0.18f });
                canvas.DrawRect(rect, NpcColor, filled: false, width: 1f);
                if (tilePixels >= MinLabelTilePixels)
                {
                    canvas.DrawString(font, rect.Position + new Vector2(2f, -3f), def.Id,
                        HorizontalAlignment.Left, -1f, 10, NpcColor);
                }
            }
        }
    }

    private static void DrawPlacements(
        Control canvas, Transform2D toCanvas, MapStage stage, int selected, float tilePixels)
    {
        Font font = canvas.GetThemeDefaultFont();
        IReadOnlyList<MapPlacement> placements = stage.Recipe.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            MapPlacement placement = placements[i];
            bool isSelected = i == selected;
            Rect2 footprint = Screen(toCanvas, PlacementFootprint.Of(placement));
            Rect2 anchor = Screen(toCanvas, new Rect2I(placement.Cell, Vector2I.One));

            canvas.DrawRect(footprint, isSelected ? SelectedColor : PlacementColor,
                filled: false, width: isSelected ? 2f : 1f);
            // The anchor cell is filled because it is the record's actual coordinate —
            // the number in the file, and the one a drag moves. A tree's outline covers
            // twelve cells; exactly one of them is what "x, y" means.
            canvas.DrawRect(anchor, (isSelected ? SelectedColor : AnchorColor) with { A = 0.30f });

            if (isSelected || tilePixels >= MinLabelTilePixels)
            {
                canvas.DrawString(font, footprint.Position + new Vector2(2f, -3f),
                    $"{placement.Kind}:{placement.Id}", HorizontalAlignment.Left, -1f, 10,
                    isSelected ? SelectedColor : LabelColor);
            }
        }
    }

    // ------------------------------------------------------------------
    // Tiles -> stage pixels -> viewport pixels
    // ------------------------------------------------------------------

    private static Vector2 Pixels(int tileX, int tileY) =>
        new(tileX * MapRoot.TileSize, tileY * MapRoot.TileSize);

    // Corner by corner rather than transforming the Rect2 itself: a Rect2 through a
    // transform is only still a rectangle while nothing rotates, and going through the
    // corners costs nothing and cannot be wrong.
    private static Rect2 Screen(Transform2D toCanvas, Rect2I tiles)
    {
        Vector2 topLeft = toCanvas * Pixels(tiles.Position.X, tiles.Position.Y);
        Vector2 bottomRight = toCanvas * Pixels(tiles.End.X, tiles.End.Y);
        return new Rect2(topLeft, bottomRight - topLeft);
    }
}
#endif
