namespace TheHaunt.Content;

/// <summary>
/// The gas station (west entry, across the road from the motor court).
/// CANON (Kevin, 2026-08-26): sells various things to be sorted out later — food, of
/// course, and some of the pills and other things gas stations sell may be useful
/// for missions, but come with side effects. Dennis works the counter.
/// PLANNED: the catalog itself — nothing here sells anything yet; the mission-useful
/// pills and their side effects need a focused design (Kevin, 2026-09-01 / D6).
///
/// The staffed window below is shared between Dennis's schedule and the west entry's
/// OPEN neon (the window mount doubles as the shop-hours tell), so the sign can
/// never lie about whether Dennis is at the counter.
/// </summary>
public static class GasStation
{
    public const int OpenMinute = 60, CloseMinute = 1140;   // 7:00 AM - 1:00 AM

    // [KEVIN] placeholder copy — canon restatement only, no names.
    public const string RoadSignText = "Gas.";
}
