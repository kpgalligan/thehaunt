namespace TheHaunt.Core;

/// <summary>
/// The barn's repair state as the art draws it: 0 derelict, 1 weathertight, 2 restored
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
