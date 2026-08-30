namespace TheHaunt.Core;

/// <summary>
/// TEMPORARY test scaffolding (Kevin, 2026-08-30): "To test for now, start each
/// day with $150k. That will allow me to buy the garage." Every dawn the
/// overnight sim tops Money UP to this floor (never down — earnings above it are
/// kept, and the floor runs BEFORE the night's income so shipping and garage
/// lines stay visible on top), and NewGame starts at the floor so day 1 can buy.
/// Delete this class and its two call sites (OvernightSim.Run step 0,
/// GameData.NewGame) to end the scaffold; the money-assertion tests account for
/// it and will point at every spot that needs re-pinning.
/// </summary>
public static class DevScaffold
{
    public const long DailyMoneyFloor = 150_000;
}
