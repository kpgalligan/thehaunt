namespace TheHaunt.Core;

/// <summary>One repair the garage offers. Work is the job's size in work units,
/// deliberately equal to the price in gold — Kevin's "the amount of work required
/// should be proportional to the cost" made literal, so a new service needs one
/// line and no tuning table.</summary>
public sealed record GarageServiceDef(string Id, string Name, long Price, int Work);

/// <summary>
/// The garage's service list (Kevin, 2026-08-30) — a starting set, "a more
/// complete list of services later". Insertion order is canonical: it is both the
/// UI listing order and the customer roll's index table
/// (<see cref="GarageOpsRules.CustomerRoll"/>), so REORDERING entries reshuffles
/// which service every deterministic arrival brings.
/// </summary>
public static class GarageServices
{
    public const string OilChange = "oil_change";
    public const string Lights = "lights";
    public const string Transmission = "transmission";

    public static IReadOnlyList<GarageServiceDef> All { get; } = new[]
    {
        new GarageServiceDef(OilChange, "Oil change", 100, 100),               // [KEVIN] $100
        new GarageServiceDef(Lights, "Fix headlight/taillight", 150, 150),     // [KEVIN] $150
        new GarageServiceDef(Transmission, "Fix transmission", 350, 350),      // [KEVIN] $350
    };

    /// <summary>Null-tolerant lookup for ids coming from save files.</summary>
    public static GarageServiceDef? TryGet(string id)
    {
        foreach (GarageServiceDef def in All)
        {
            if (def.Id == id)
            {
                return def;
            }
        }
        return null;
    }
}
