namespace TheHaunt.Core;

/// <summary>Outcome of one work press on a lift. Worked/CompletedJob mutated;
/// everything else refused with NOTHING touched (mutation discipline) — with ONE
/// exception: a hostile full-work-but-not-Completed record self-heals to
/// Completed on its AlreadyDone answer (charge-free, XP-free; load repair closes
/// the same limbo, so live play never reaches it).</summary>
public enum GarageWorkResult { Worked, CompletedJob, NoJob, AlreadyDone, NotEnoughStamina, NotOwned }

/// <summary>
/// The owned garage's operating rules (Kevin, 2026-08-30). Open 7 days, 9 AM to
/// 6 PM — the window gates customer ARRIVALS and Mike's presence only; Jane can
/// walk in and work at any hour. Every open hour rolls a 6% chance of a customer
/// (deterministic per (save seed, day, hour) — <see cref="CustomerRoll"/>), at
/// most <see cref="MaxCars"/> cars in the shop at once, and a car occupies its
/// lift until the overnight sim resolves the job.
///
/// The work model: one E press on an occupied lift banks
/// <see cref="WorkPerPress"/> units and costs <see cref="StaminaPerPress"/>
/// stamina. WorkPerPress(level) = level + 5 makes a full 100-stamina day worth
/// exactly 300 + 50·(level−1) units — Kevin's anchor numbers: 3 oil changes/day
/// at level 1, half an oil change more per level, 7.5 at level 10. The final
/// press of a job charges pro-rata (rounded, min 1), which is load-bearing:
/// a flat 2 per press would ceil an L1 oil change to 34 stamina and break
/// "exactly 3 per day".
/// </summary>
public static class GarageOpsRules
{
    public const int MaxCars = 2;

    /// <summary>Open window in AbsoluteHour terms: [9, 18) — 9 AM through the
    /// 5 o'clock hour, closing at 6 PM. Nine arrival rolls per day.</summary>
    public const int OpenHour = 9;     // [KEVIN] 9:00 AM
    public const int CloseHour = 18;   // [KEVIN] 6:00 PM, exclusive

    /// <summary>The same window in minutes-of-day, for ScheduleEntry (minute 0 =
    /// 6:00 AM) — derived from the hours above so Mike's presence and the arrival
    /// window can never diverge (the Shopkeeper↔ShopHours binding).</summary>
    public const int OpenMinuteOfDay = (OpenHour - GameTime.DayStartHour) * 60;    // 180
    public const int CloseMinuteOfDay = (CloseHour - GameTime.DayStartHour) * 60;  // 720

    public const int ArrivalPercent = 6;   // [KEVIN] "6% chance that a new customer will come in"

    public static bool IsOpenHour(int absoluteHour) =>
        absoluteHour >= OpenHour && absoluteHour < CloseHour;

    public const int StaminaPerPress = 2;   // a full press costs an axe swing

    /// <summary>Work units banked per full press: 6 at level 1 up to 15 at level 10.</summary>
    public static int WorkPerPress(int level) => Math.Clamp(level, 1, SkillRules.MaxLevel) + 5;

    /// <summary>
    /// The hourly customer roll — pure hash of (save seed, day, hour), so replaying
    /// an hour can never re-roll a different answer and tests pin exact schedules.
    /// Self-owned mixer (the ObstacleGen precedent: a .NET Random rewrite must not
    /// reshuffle saves or tests). ServiceIndex indexes <see cref="GarageServices.All"/>.
    /// </summary>
    public static (bool Arrived, int ServiceIndex) CustomerRoll(int seed, long day, int hour)
    {
        // Golden-ratio offsets keep seed 0 (every migrated save) cascading.
        ulong h = Mix((uint)seed + 0x9e3779b97f4a7c15UL);
        h = Mix(h ^ ((ulong)day + 0x100000001b3UL));
        h = Mix(h ^ (ulong)(uint)hour);
        bool arrived = h % 100 < ArrivalPercent;
        int service = (int)((h >> 32) % (ulong)GarageServices.All.Count);
        return (arrived, service);
    }

    // splitmix64's finalizer — full-avalanche, so consecutive (day, hour) inputs
    // land uncorrelated.
    private static ulong Mix(ulong x)
    {
        x ^= x >> 33;
        x *= 0xff51afd7ed558ccdUL;
        x ^= x >> 33;
        x *= 0xc4ceb9fe1a85ec53UL;
        x ^= x >> 33;
        return x;
    }

    /// <summary>The job occupying a lift; null when the bay is empty.</summary>
    public static GarageJobRecord? JobAt(GameData data, int lift)
    {
        foreach (GarageJobRecord job in data.GarageJobs)
        {
            if (job.Lift == lift)
            {
                return job;
            }
        }
        return null;
    }

    /// <summary>Lowest empty bay, or null when the shop is full.</summary>
    public static int? FreeLift(GameData data)
    {
        for (int lift = 0; lift < MaxCars; lift++)
        {
            if (JobAt(data, lift) == null)
            {
                return lift;
            }
        }
        return null;
    }

    /// <summary>
    /// One work press on a lift — the pure model half of WorldSim.WorkOnGarageJob.
    /// FarmActions discipline: every check strictly before any mutation; a refusal
    /// touches nothing. Completion flips the record only — the BUS observes
    /// CompletedJob and grants the mechanical-repair XP (Core stays XP-free).
    /// </summary>
    public static GarageWorkResult DoWork(GameData data, int lift)
    {
        if (!GarageRules.IsOwned(data))
        {
            return GarageWorkResult.NotOwned;
        }
        GarageJobRecord? job = JobAt(data, lift);
        if (job == null)
        {
            return GarageWorkResult.NoJob;
        }
        if (job.Completed)
        {
            return GarageWorkResult.AlreadyDone;
        }
        GarageServiceDef? service = GarageServices.TryGet(job.ServiceId);
        if (service == null)
        {
            // Load repair drops unknown-service jobs; a live one is a code bug,
            // but an unworkable car must answer like an empty bay, not crash.
            return GarageWorkResult.NoJob;
        }

        int remaining = service.Work - job.WorkDone;
        if (remaining <= 0)
        {
            // Load repair marks these Completed; self-heal the impossible state
            // without charging stamina or paying XP for work that never happened.
            job.Completed = true;
            return GarageWorkResult.AlreadyDone;
        }

        int perPress = WorkPerPress(SkillRules.Level(data, SkillIds.MechanicalRepair));
        int done = Math.Min(perPress, remaining);
        // Pro-rata final press, rounded half-up, never free — see class doc.
        int cost = Math.Max(1, (StaminaPerPress * done + perPress / 2) / perPress);
        if (data.Player.Stamina < cost)
        {
            return GarageWorkResult.NotEnoughStamina;
        }

        job.WorkDone += done;
        data.Player.Stamina -= cost;
        if (job.WorkDone >= service.Work)
        {
            job.Completed = true;
            return GarageWorkResult.CompletedJob;
        }
        return GarageWorkResult.Worked;
    }
}
