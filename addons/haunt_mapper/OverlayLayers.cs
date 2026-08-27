#if TOOLS
namespace TheHaunt.Addons.HauntMapper;

/// <summary>
/// Which viewport overlays the mapper draws. Every one of these renders something that
/// is COMPLETELY invisible in a running game and in the preview alike — the tile grid a
/// placement snaps to, the cells the hoe silently refuses, the transparent blockers under
/// every sprite-drawn facade, the slots an NPC teleports into. That invisibility is the
/// whole reason placing things by hand has been guesswork: nothing you can see tells you
/// where the thing you are dragging actually lands.
///
/// Flags rather than a bag of bools so the set travels as one value between the dock, the
/// plugin and the draw pass, and so adding one is a constant rather than three edits.
/// </summary>
[Flags]
public enum OverlayLayers
{
    None = 0,

    /// <summary>The 16px tile grid, over the map's used rect only. Suppressed when zoomed out past legibility.</summary>
    Grid = 1 << 0,

    /// <summary>Every placement's footprint, plus its anchor cell — the thing there is to grab.</summary>
    Placements = 1 << 1,

    /// <summary>MapRoot.ReservedTiles(): tillable ground held back for a reason no tile shows.</summary>
    Reserved = 1 << 2,

    /// <summary>Painted cells on the Obstacles layer — the transparent blockers that do the colliding.</summary>
    Blockers = 1 << 3,

    /// <summary>Every tile any NpcSchedules entry can stage an NPC onto, for this map.</summary>
    NpcSlots = 1 << 4,

    /// <summary>What a fresh dock starts with: enough to place by, without burying the art.</summary>
    Default = Grid | Placements | Reserved,
}
#endif
