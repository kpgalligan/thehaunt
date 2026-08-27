using Godot;

namespace TheHaunt.World;

/// <summary>
/// A building that exists in the story before it exists in the art: a flat-front
/// elevation face built in code, in the muted flat colors the pre-art town used, so a
/// new frame can ship the day it is designed. Same contract as every drawn facade —
/// base-anchored front face, no side walls, doorway drawn one tile above the plinth
/// row, collision left to the map's Blocker cells — so the day real art lands it is
/// replaced by a Prop with the same Position and nothing else moves.
/// </summary>
public partial class PlaceholderBuilding : Sprite2D
{
    /// <summary>Footprint width in tiles.</summary>
    public int TilesWide { get; init; } = 4;

    /// <summary>Rows of blocked footprint. The drawn face adds two rows of roof above,
    /// the overhang every real facade has.</summary>
    public int FootprintRows { get; init; } = 3;

    /// <summary>Front-face color; roof, plinth and openings are derived shades.</summary>
    public Color Wall { get; init; } = new("9a9a8a");

    /// <summary>True for a building shut so long the doorway is planked over (the
    /// drive-in's concession stand): no Door node, no handle — the boards ARE the
    /// answer, so the face has to show them.</summary>
    public bool Boarded { get; init; }

    private const int RoofRows = 2;

    public override void _Ready()
    {
        int w = TilesWide * MapRoot.TileSize;
        int h = (FootprintRows + RoofRows) * MapRoot.TileSize;
        Offset = new Vector2(0, -h / 2f);
        Texture = Build(w, h);
    }

    private ImageTexture Build(int w, int h)
    {
        Color roof = Wall.Darkened(0.4f);
        Color eave = Wall.Darkened(0.55f);
        Color plinth = Wall.Darkened(0.2f);
        Color opening = Wall.Darkened(0.7f);

        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(Wall);

        // Roof band over the two overhang rows, with a 1px eave shadow under it.
        img.FillRect(new Rect2I(0, 0, w, RoofRows * 16 - 2), roof);
        img.FillRect(new Rect2I(0, RoofRows * 16 - 2, w, 2), eave);

        // Stone plinth: the bottom row the doorway convention keeps the player off.
        img.FillRect(new Rect2I(0, h - 16, w, 16), plinth);

        // A shut door on the row above the plinth, centered on the face — or the
        // grey plywood over where it was.
        img.FillRect(new Rect2I(w / 2 - 4, h - 30, 8, 14), opening);
        if (Boarded)
        {
            Color plank = Wall.Darkened(0.35f);
            for (int py = 0; py < 4; py++)
                img.FillRect(new Rect2I(w / 2 - 5, h - 29 + py * 4, 10, 2), plank);
        }

        // Dark windows every other tile, skipping the door's tile and the ends.
        int windowRow = h - 16 * (FootprintRows > 2 ? 3 : 2) + 4;
        for (int tx = 1; tx < TilesWide - 1; tx++)
        {
            int cx = tx * 16 + 8;
            if (FootprintRows <= 2 && Mathf.Abs(cx - w / 2) < 12)
                continue; // a low building's windows share the door row — keep clear of it
            if (tx % 2 == 1)
                img.FillRect(new Rect2I(cx - 3, windowRow, 6, 9), opening);
        }

        return ImageTexture.CreateFromImage(img);
    }
}
