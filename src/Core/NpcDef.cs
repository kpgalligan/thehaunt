namespace TheHaunt.Core;

// Ambit: how far (in tiles, Chebyshev) the view may amble from this staging tile —
// 0 is a fixture that never moves. VIEW-side flavour only: the model's answer to
// "where is this NPC" is always the staging tile, and dialogue/probe reads never
// depend on the amble.
public readonly record struct NpcPlacement(string MapId, int TileX, int TileY, int Facing, int Ambit = 0);

public sealed record ScheduleEntry(
    string? RequiresFlag, string? ForbidsFlag,
    int StartMinuteOfDay, int EndMinuteOfDay,      // inclusive / exclusive; 0..1200, no wrap
    NpcPlacement Placement);

// SpriteSheet is a res:// path to a cast sheet (cast-sprites handoff);
// SpriteBlock is the character's 96px-wide block index inside it.
public sealed record NpcDef(string Id, string DisplayRole, string SpriteSheet, int SpriteBlock,
    IReadOnlyList<ScheduleEntry> Schedule);        // FIRST match wins
