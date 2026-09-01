using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The Shopkeeper — the general store's counter. Role-labeled by design (Kevin,
/// 2026-09-01 / D3); the store's NAME is deliberately unwritten too.
/// Silent this phase [KEVIN] — the counter (ShopCounter -> the buy session) is the
/// whole interaction; a voice is an open seam.
/// Look: cast_town block 4 (button-down and canvas work apron).
/// </summary>
public static class Shopkeeper
{
    public const string Id = "shopkeeper";

    // Flag-free and bound to the ShopHours constants so shop-open and
    // shopkeeper-present can never diverge; absent outside hours, never on
    // farm/town/town_hall (intro staging untouched).
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            ShopHours.OpenMinute, ShopHours.CloseMinute,     // 9:00 AM - 5:00 PM
            new NpcPlacement(MapIds.GeneralStore, 6, 3, 0, Ambit: 1)), // behind the counter, facing down
    };

    public static string? Talk(GameData data, GameTime now) => null;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Shopkeeper", CastSheets.Town, 4, Schedule), Talk,
        Array.Empty<DialogueDef>(), SilentByDesign: true);
}
