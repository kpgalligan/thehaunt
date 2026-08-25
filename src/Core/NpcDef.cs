namespace TheHaunt.Core;

public readonly record struct NpcPlacement(string MapId, int TileX, int TileY, int Facing);

public sealed record ScheduleEntry(
    string? RequiresFlag, string? ForbidsFlag,
    int StartMinuteOfDay, int EndMinuteOfDay,      // inclusive / exclusive; 0..1200, no wrap
    NpcPlacement Placement);

public sealed record NpcDef(string Id, string DisplayRole, string BodyColor,
    IReadOnlyList<ScheduleEntry> Schedule);        // FIRST match wins
