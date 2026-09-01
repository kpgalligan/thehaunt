namespace TheHaunt.Core;

/// <summary>
/// One clearable field obstacle kind: which tool works it, how many effective hits it
/// takes, what it yields when the last hit lands, and what (if anything) it leaves
/// behind. Stamina is charged per effective hit from the TOOL's own StaminaCost; the
/// yield is granted only on the final hit, all-or-nothing against inventory room —
/// the harvest precedent.
/// </summary>
public sealed record ObstacleDef(
    string Id,
    ToolKind Tool,
    int Hits,
    string YieldItemId,
    int YieldCount,
    string? BecomesId = null);
