using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The repair garage beside the west entry's gas station — Jane's route back into
/// her father's trade (Characters/Jane, Characters/Mike). The RULES stay in Core,
/// where the operation lives: GarageRules (the sale — price, the deed),
/// GarageOpsRules (hours, arrivals, the work press curve), GarageServices (the
/// service list); this file holds the place's copy.
/// CANON (Kevin, 2026-08-26 + the 2026-08-29/30 commissions): starts closed and
/// FOR SALE; once owned it runs 7 days, 9-6 — the hours gate customers and Mike,
/// never Jane, whose door answers with a line until the deed is stamped.
/// </summary>
public static class Garage
{
    public const string MapId = MapIds.GarageInterior;

    // [KEVIN] locked-handle line — the shut shop door before the deed. A locked
    // handle answers with a line, never silence (the motel-room rule).
    public const string DoorLockedLine = "Closed.";
}
