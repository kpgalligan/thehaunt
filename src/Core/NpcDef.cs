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

public sealed record NpcDef(string Id, string DisplayRole, string BodyColor,
    IReadOnlyList<ScheduleEntry> Schedule);        // FIRST match wins
