using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Geometry and loading for the drawn character sheets (cast-sprites handoff).
/// Every character — Jane (assets/sprites/character.png) and the cast atlases under
/// assets/sprites/cast/ — is a 96x96 block of 16x32 cells: 6 columns (cols 0-1 idle,
/// cols 2-5 walk) by 3 rows (down/left/up), feet on the bottom row of every cell.
/// Identity is the sheet + block an <see cref="NpcDef"/> names; the old
/// tunic-recolor channel is gone (NpcDef.BodyColor died with it).
/// </summary>
public static class CharacterSprites
{
    public const string SheetPath = "res://assets/sprites/character.png";

    /// <summary>
    /// The riding sheet (scooter handoff): same 96x96 grid, same rows — the rider IS
    /// character.png composited 6px higher onto the deck, regenerated from it by
    /// tools/regen_scooter_rider.py whenever Jane's walk sheet changes. All six
    /// columns are one motion cycle.
    /// </summary>
    public const string RiderSheetPath = "res://assets/sprites/scooter_rider.png";

    public const int RiderFrames = 6;

    public const int CellWidth = 16;
    public const int CellHeight = 32;
    public const int BlockWidth = 6 * CellWidth;   // one character in a packed atlas
    public const int IdleFrames = 2;   // cols 0-1
    public const int WalkFrames = 4;   // cols 2-5

    // Facing 2 (right) is a horizontal flip of facing 1 (left) — every sheet holds
    // down/left/up only.
    private static readonly int[] RowByFacing = { 0, 1, 1, 2 };

    /// <summary>Row 0=down, 1=left, 2=up. Facing 2 (right) reuses row 1, mirrored.</summary>
    public static int Row(int facing) => RowByFacing[Math.Clamp(facing, 0, 3)];

    public static bool FlipH(int facing) => facing == 2;

    /// <summary>
    /// The riding sheet's profile row is authored facing RIGHT (the handoff recipe
    /// puts the front wheel at x=12 and the headlamp at x=14) — mirrored from the
    /// walk sheets' left-facing row — so it flips for LEFT where the walk sheets
    /// flip for right.
    /// </summary>
    public static bool RiderFlipH(int facing) => facing == 1;

    /// <summary>
    /// Source rect for one cell. <paramref name="block"/> is the character's 96px
    /// block index in the sheet (0 for standalone sheets); <paramref name="column"/> is 0-5.
    /// </summary>
    public static Rect2 Region(int block, int facing, int column) => new(
        block * BlockWidth + column * CellWidth, Row(facing) * CellHeight, CellWidth, CellHeight);

    public static Texture2D Sheet(string path) => GD.Load<Texture2D>(path)
        ?? throw new InvalidOperationException($"Character sheet missing at '{path}'.");

    // ---- Tool work sheets (tools-animations handoff) ----------------------
    // 64x192 per tool: 4 columns (windup, strike, impact, recover) by 6 rows —
    // row = tier * 2 + (0 down, 1 side); tiers 0 basic, 1 dad-level, 2 pro. Up
    // rows are not authored and reuse down. The animation is baked into Jane's
    // frames (no overlay layer), so working is a sheet + row selection like
    // riding is. CAUTION: like the scooter sheets, the side rows swing on the
    // figure's RIGHT (the handoff's row table) and flip for LEFT — mirrored
    // from the walk sheets' left-facing convention.

    public const int WorkFrames = 4;
    public const int WorkTiers = 3;

    /// <summary>The tool's work sheet, or null for tools with no authored work
    /// animation (the scythe stays on the instant-use path).</summary>
    public static string? WorkSheet(ToolKind tool) => tool switch
    {
        ToolKind.Hoe => "res://assets/sprites/tools/tool_hoe.png",
        ToolKind.WateringCan => "res://assets/sprites/tools/tool_can.png",
        ToolKind.Axe => "res://assets/sprites/tools/tool_axe.png",
        ToolKind.Pick => "res://assets/sprites/tools/tool_pick.png",
        _ => null,
    };

    /// <summary>Source rect for one work cell. Facing up reuses the down row.</summary>
    public static Rect2 WorkRegion(int tier, int facing, int frame) => new(
        frame * CellWidth,
        (tier * 2 + (Row(facing) == 1 ? 1 : 0)) * CellHeight,
        CellWidth, CellHeight);

    public static bool WorkFlipH(int facing) => facing == 1;
}
