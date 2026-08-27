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

    /// <summary>
    /// The riding sheet (scooter handoff): same 96x96 grid, same rows, same
    /// flip-for-right — the rider IS character.png composited 6px higher onto the
    /// deck, so it recolors through the same tunic swap (the deck greens are nowhere
    /// near the authored plum). All six columns are one motion cycle.
    /// </summary>
    public const string RiderSheetPath = "res://assets/sprites/scooter_rider.png";

    public const int RiderFrames = 6;

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
    private static readonly Dictionary<Color, Texture2D> RecoloredRider = new();

    /// <summary>Row 0=down, 1=left, 2=up. Facing 2 (right) reuses row 1, mirrored.</summary>
    public static int Row(int facing) => RowByFacing[Math.Clamp(facing, 0, 3)];

    public static bool FlipH(int facing) => facing == 2;

    /// <summary>
    /// The riding sheet's profile row is authored facing RIGHT (the handoff recipe
    /// puts the front wheel at x=12 and the headlamp at x=14) — mirrored from the
    /// walk sheet's left-facing row — so it flips for LEFT where the walk sheet
    /// flips for right.
    /// </summary>
    public static bool RiderFlipH(int facing) => facing == 1;

    /// <summary>Source rect for one cell. <paramref name="column"/> is 0-5.</summary>
    public static Rect2 Region(int facing, int column) => new(
        column * CellWidth, Row(facing) * CellHeight, CellWidth, CellHeight);

    /// <summary>
    /// The sheet with its tunic swapped for <paramref name="tunic"/>. The authored
    /// tunic color is matched exactly — the sheet is a flat palette with no ramp on
    /// the coat, so a single-color swap is the whole job.
    /// </summary>
    public static Texture2D Sheet(Color tunic) => Recolor(SheetPath, tunic, Recolored);

    /// <summary>The riding sheet with the same tunic swap applied.</summary>
    public static Texture2D RiderSheet(Color tunic) => Recolor(RiderSheetPath, tunic, RecoloredRider);

    private static Texture2D Recolor(string path, Color tunic, Dictionary<Color, Texture2D> cache)
    {
        if (cache.TryGetValue(tunic, out Texture2D? cached))
            return cached;

        var source = GD.Load<Texture2D>(path)
            ?? throw new InvalidOperationException($"Character sheet missing at '{path}'.");

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

        cache[tunic] = result;
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
