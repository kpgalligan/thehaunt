using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The barn came with the farm and is falling down — run down and empty (canon,
/// Kevin 2026-08-26), holding only the chest with the previous owner's starter kit.
///
/// Its repair state as the art draws it: 0 derelict, 1 weathertight, 2 restored
/// (farm/interiors handoff §4). Three states, not a percentage — a completion slider
/// would need art for every value in between, and three clean reads is what was drawn.
///
/// The handoff proposes storing 0/1/2 in a single story flag. It cannot: a flag's value
/// in <see cref="GameData.StoryFlags"/> is the day index it was stamped, and flags are
/// monotone with absence meaning false. Two monotone flags carry the same three states
/// with no schema change and no way to go backwards.
///
/// What ADVANCES the state is deliberately not invented here. There is no barn repair
/// mechanic in the game yet; this is the seam it will write through.
/// </summary>
public static class BarnRules
{
    public const int Derelict = 0;
    public const int Weathertight = 1;
    public const int Restored = 2;

    public static int StateOf(GameData data) =>
        data.HasFlag(StoryKeys.BarnRestored) ? Restored
        : data.HasFlag(StoryKeys.BarnWeathertight) ? Weathertight
        : Derelict;
}
