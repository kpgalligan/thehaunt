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
            or MapIds.FarmHouse or MapIds.GeneralStore or MapIds.Barn
            or MapIds.WestEntry or MapIds.Billies or MapIds.Fork
            or MapIds.EastFork or MapIds.EastEntry
            or MapIds.Motel or MapIds.GasStation or MapIds.BilliesBar or MapIds.Salon
            or MapIds.MotelRoom1 or MapIds.MotelRoom2 or MapIds.MotelRoom3
            or MapIds.MotelRoom4 or MapIds.DriveIn;

    public static MapRoot Create(string mapId) => mapId switch
    {
        MapIds.Farm => new TestMap(),
        MapIds.Town => new TownMap(),
        MapIds.TownHall => new TownHallMap(),
        MapIds.FarmHouse => new FarmHouseMap(),
        MapIds.GeneralStore => new GeneralStoreMap(),
        MapIds.Barn => new BarnMap(),
        MapIds.WestEntry => new WestEntryMap(),
        MapIds.Billies => new BilliesMap(),
        MapIds.Fork => new ForkMap(),
        MapIds.EastFork => new EastForkMap(),
        MapIds.EastEntry => new EastEntryMap(),
        MapIds.Motel => new MotelMap(),
        MapIds.GasStation => new GasStationMap(),
        MapIds.BilliesBar => new BilliesBarMap(),
        MapIds.Salon => new SalonMap(),
        MapIds.MotelRoom1 => new MotelRoomMap { RoomNumber = 1 },
        MapIds.MotelRoom2 => new MotelRoomMap { RoomNumber = 2 },
        MapIds.MotelRoom3 => new MotelRoomMap { RoomNumber = 3 },
        MapIds.MotelRoom4 => new MotelRoomMap { RoomNumber = 4 },
        MapIds.DriveIn => new DriveInMap(),
        _ => throw new ArgumentException($"Unknown map id '{mapId}'.", nameof(mapId)),
    };
}
