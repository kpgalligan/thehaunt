using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Map id -> root-node factory. Becomes PackedScene.Instantiate with zero
/// call-site change once editor-authored maps land. Unknown ids throw —
/// Main.LoadMap guards and falls back to the farm, leaving the unknown map's
/// MapState untouched in GameData.Maps (preservation rule).
/// </summary>
public static class MapRegistry
{
    public static bool Contains(string mapId) =>
        mapId is MapIds.Farm or MapIds.Town or MapIds.TownHall
            or MapIds.FarmHouse or MapIds.GeneralStore or MapIds.Barn;

    public static MapRoot Create(string mapId) => mapId switch
    {
        MapIds.Farm => new TestMap(),
        MapIds.Town => new TownMap(),
        MapIds.TownHall => new TownHallMap(),
        MapIds.FarmHouse => new FarmHouseMap(),
        MapIds.GeneralStore => new GeneralStoreMap(),
        MapIds.Barn => new BarnMap(),
        _ => throw new ArgumentException($"Unknown map id '{mapId}'.", nameof(mapId)),
    };
}
