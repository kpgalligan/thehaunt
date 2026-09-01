namespace TheHaunt.Core;

public static class ObstacleDefs
{
    public const string Tree = "tree";
    public const string Stump = "stump";
    public const string Rock = "rock";

    // A felled tree leaves its stump, and the stump is worth cutting too (Kevin,
    // 2026-08-28: "a tree is cut and produces lumber and leaves a stump; the stump
    // can then be cut and removed for more lumber"). Rocks break outright.
    public static IReadOnlyDictionary<string, ObstacleDef> All { get; } = new[]
    {
        new ObstacleDef(Tree, ToolKind.Axe, Hits: 5, YieldItemId: "lumber", YieldCount: 5, BecomesId: Stump),
        new ObstacleDef(Stump, ToolKind.Axe, Hits: 2, YieldItemId: "lumber", YieldCount: 2),
        new ObstacleDef(Rock, ToolKind.Pick, Hits: 3, YieldItemId: "stone", YieldCount: 2),
    }.ToDictionary(d => d.Id);

    /// <summary>
    /// Null-tolerant lookup: <see cref="MapState.Objects"/> is a shared seam, so an
    /// ObjectId this table does not know (a chest, a newer build's obstacle) is not an
    /// obstacle here — it is preserved untouched, never struck and never destroyed.
    /// </summary>
    public static ObstacleDef? TryGet(string? objectId) =>
        objectId is not null && All.TryGetValue(objectId, out var def) ? def : null;
}
