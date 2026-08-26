using Godot;

namespace TheHaunt.World;

/// <summary>
/// Procedural 16x22 character sprites. Superseded for the player and NPCs by the
/// drawn sheet behind <see cref="CharacterSprite"/>, and deliberately kept: the art
/// handoff asks that the procedural placeholders keep working so a new map or cast
/// member can ship before its art exists. Hair, skin, and eye colors are common
/// stock; the tunic color is what tells characters apart.
/// </summary>
public static class PlaceholderSprites
{
    public const int Width = 16;
    public const int Height = 22;

    private static readonly Color HairColor = new("5a4a3a");
    private static readonly Color SkinColor = new("e8c8a0");
    private static readonly Color EyeColor = new("2a2a2a");

    // 16x22 placeholder: hair on top, face below, tunic for the body, 2 px of
    // transparent margin on each side. Facing up shows the back of the head
    // (hair extends over the face rows); eyes shift with left/right facing.
    public static ImageTexture Character(int facing, Color tunic)
    {
        var img = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        int hairBottomRow = facing == 3 ? 9 : 5;
        for (int y = 0; y < Height; y++)
        {
            Color color = y <= hairBottomRow ? HairColor
                : y <= 11 ? SkinColor
                : tunic;
            for (int x = 2; x <= 13; x++)
                img.SetPixel(x, y, color);
        }

        if (facing != 3)
        {
            int offset = facing switch { 1 => -2, 2 => 2, _ => 0 };
            foreach (int eyeX in new[] { 5 + offset, 10 + offset })
            {
                img.SetPixel(eyeX, 8, EyeColor);
                img.SetPixel(eyeX, 9, EyeColor);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
