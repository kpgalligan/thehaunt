using Godot;

namespace TheHaunt.World;

/// <summary>
/// The 16x32 character walk sheet (assets/sprites/character.png): 6 frames x 3
/// directions, cols 0-1 idle and cols 2-5 walk, feet on the bottom row of every cell.
///
/// The sheet is drawn with one tunic color; per-character tunics stay the secondary
/// identity channel (art bible §06) by recoloring that one palette entry, so every
/// character shares the same silhouette and outline. Recolored sheets are cached per
/// tunic — five NPCs plus the player is five images for the life of the process.
/// </summary>
public static class CharacterSprites
{
    public const string SheetPath = "res://assets/sprites/character.png";

    public const int CellWidth = 16;
    public const int CellHeight = 32;
    public const int IdleFrames = 2;   // cols 0-1
    public const int WalkFrames = 4;   // cols 2-5

    /// <summary>The tunic color the sheet is authored in (palette `plum`).</summary>
    private static readonly Color SheetTunic = new("6b4560");

    // Facing 2 (right) is a horizontal flip of facing 1 (left) — the sheet holds
    // down/left/up only.
    private static readonly int[] RowByFacing = { 0, 1, 1, 2 };

    private static readonly Dictionary<Color, Texture2D> Recolored = new();

    /// <summary>Row 0=down, 1=left, 2=up. Facing 2 (right) reuses row 1, mirrored.</summary>
    public static int Row(int facing) => RowByFacing[Math.Clamp(facing, 0, 3)];

    public static bool FlipH(int facing) => facing == 2;

    /// <summary>Source rect for one cell. <paramref name="column"/> is 0-5.</summary>
    public static Rect2 Region(int facing, int column) => new(
        column * CellWidth, Row(facing) * CellHeight, CellWidth, CellHeight);

    /// <summary>
    /// The sheet with its tunic swapped for <paramref name="tunic"/>. The authored
    /// tunic color is matched exactly — the sheet is a flat palette with no ramp on
    /// the coat, so a single-color swap is the whole job.
    /// </summary>
    public static Texture2D Sheet(Color tunic)
    {
        if (Recolored.TryGetValue(tunic, out Texture2D? cached))
            return cached;

        var source = GD.Load<Texture2D>(SheetPath)
            ?? throw new InvalidOperationException($"Character sheet missing at '{SheetPath}'.");

        Texture2D result;
        if (tunic.IsEqualApprox(SheetTunic))
        {
            result = source;
        }
        else
        {
            // Copy the pixels out before touching them. Texture2D.GetImage() hands back
            // the texture's own image under the headless renderer (only the GPU path
            // copies), and recoloring in place would leave every later tunic reading a
            // sheet that no longer has the authored color in it.
            Image sheet = source.GetImage();
            Image image = Image.CreateFromData(
                sheet.GetWidth(), sheet.GetHeight(), false, sheet.GetFormat(), sheet.GetData());
            image.Convert(Image.Format.Rgba8);
            for (int y = 0; y < image.GetHeight(); y++)
            {
                for (int x = 0; x < image.GetWidth(); x++)
                {
                    Color pixel = image.GetPixel(x, y);
                    if (pixel.A > 0f && IsSheetTunic(pixel))
                        image.SetPixel(x, y, new Color(tunic, pixel.A));
                }
            }
            result = ImageTexture.CreateFromImage(image);
        }

        Recolored[tunic] = result;
        return result;
    }

    // The PNG round-trip leaves a handful of +-1 variants of the authored tunic;
    // match on a tight distance rather than equality so none of them survive as
    // speckles of the old color.
    private static bool IsSheetTunic(Color pixel) =>
        Mathf.Abs(pixel.R - SheetTunic.R) < 0.01f
        && Mathf.Abs(pixel.G - SheetTunic.G) < 0.01f
        && Mathf.Abs(pixel.B - SheetTunic.B) < 0.01f;
}
