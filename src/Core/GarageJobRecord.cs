namespace TheHaunt.Core;

/// <summary>
/// One customer's car in the shop — a mutable save DTO in the *Record tradition
/// (ItemStackRecord, TileRecord). Lives in <see cref="GameData.GarageJobs"/> from
/// the hourly arrival roll until the overnight sim resolves it: paid and removed
/// at the dawn after completion, or reclaimed unpaid at dawn of ArrivalDay + 2.
/// Day-index fields, never booleans-for-time (core rule); Lift is the job's
/// STABLE bay (0..MaxCars-1), stamped at arrival so the other car's departure
/// never makes this one hop bays.
/// </summary>
public sealed class GarageJobRecord
{
    public string ServiceId { get; set; } = "";
    public long ArrivalDay { get; set; }
    public int ArrivalHour { get; set; }   // AbsoluteHour at the roll (9..17); also the re-roll guard
    public int Lift { get; set; }
    public int WorkDone { get; set; }      // work units banked so far — partial work persists across days
    public bool Completed { get; set; }    // work finished; car waits on the lift for pickup + payment next dawn
}
