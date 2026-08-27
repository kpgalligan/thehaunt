using Godot;

namespace TheHaunt.World;

/// <summary>
/// The one typeface every sign in town uses: the motel handoff's 3-wide, 5-tall pixel
/// alphabet, at 1x for small signs and 2x for headline panels. No second typeface —
/// one glyph set stays readable at 480x270 and makes a new sign nearly free to
/// author. Advance is 4px per character at 1x (3px glyph + 1px gap); at scale s, 4s.
/// </summary>
public static class PixelFont
{
    // Glyph rows, MSB left, straight from the handoff's table.
    private static readonly Dictionary<char, string> Glyphs = new()
    {
        ['A'] = "010,101,111,101,101", ['B'] = "110,101,110,101,110",
        ['C'] = "011,100,100,100,011", ['D'] = "110,101,101,101,110",
        ['E'] = "111,100,110,100,111", ['F'] = "111,100,110,100,100",
        ['G'] = "011,100,101,101,011", ['H'] = "101,101,111,101,101",
        ['I'] = "111,010,010,010,111", ['J'] = "001,001,001,101,010",
        ['K'] = "101,101,110,101,101", ['L'] = "100,100,100,100,111",
        ['M'] = "101,111,111,101,101", ['N'] = "101,111,111,111,101",
        ['O'] = "010,101,101,101,010", ['P'] = "110,101,110,100,100",
        ['Q'] = "010,101,101,110,011", ['R'] = "110,101,110,101,101",
        ['S'] = "011,100,010,001,110", ['T'] = "111,010,010,010,010",
        ['U'] = "101,101,101,101,011", ['V'] = "101,101,101,101,010",
        ['W'] = "101,101,111,111,101", ['X'] = "101,101,010,101,101",
        ['Y'] = "101,101,010,010,010", ['Z'] = "111,001,010,100,111",
        ['0'] = "111,101,101,101,111", ['1'] = "010,110,010,010,111",
        ['2'] = "110,001,010,100,111", ['3'] = "110,001,010,001,110",
        ['4'] = "101,101,111,001,001", ['5'] = "111,100,110,001,110",
        ['6'] = "011,100,110,101,010", ['7'] = "111,001,010,010,010",
        ['8'] = "010,101,010,101,010", ['9'] = "010,101,011,001,110",
        ['-'] = "000,000,111,000,000", ['.'] = "000,000,000,000,010",
        ['\''] = "010,010,000,000,000", [' '] = "000,000,000,000,000",
    };

    /// <summary>Rendered width in pixels: len * 4 * s - s (the last gap is dropped).</summary>
    public static int Measure(string text, int scale = 1) =>
        text.Length == 0 ? 0 : text.Length * 4 * scale - scale;

    public const int GlyphHeight = 5;

    /// <summary>
    /// Draws <paramref name="text"/> onto <paramref name="img"/> with its top-left at
    /// (x, y). A character with no glyph throws — a sign with a typo should fail the
    /// build, not draw a hole.
    /// </summary>
    public static void Draw(Image img, int x, int y, string text, Color color, int scale = 1)
    {
        int cx = x;
        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            if (!Glyphs.TryGetValue(c, out string? rows))
                throw new ArgumentException($"No 3x5 glyph for '{raw}'.", nameof(text));
            string[] bits = rows.Split(',');
            for (int gy = 0; gy < GlyphHeight; gy++)
                for (int gx = 0; gx < 3; gx++)
                    if (bits[gy][gx] == '1')
                        img.FillRect(new Rect2I(cx + gx * scale, y + gy * scale, scale, scale), color);
            cx += 4 * scale;
        }
    }

    /// <summary>Draws centred on <paramref name="centerX"/>.</summary>
    public static void DrawCentered(Image img, int centerX, int y, string text, Color color, int scale = 1) =>
        Draw(img, centerX - Measure(text, scale) / 2, y, text, color, scale);
}
