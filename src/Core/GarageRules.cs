namespace TheHaunt.Core;

public enum GarageSaleResult { Ok, NotOpen, AlreadyOwned, InsufficientFunds }

/// <summary>
/// The repair garage beside the west entry's gas station (docs/story/README.md
/// §West entry): for sale until Jane buys it, and ownership is the day-stamped
/// garage.deed flag — one monotone flag, never a level. The asking price is
/// Kevin's "$100k, for now". The deed is read by the whole operation layer
/// (2026-08-30): the deed-locked door into GarageInteriorMap, Mike's schedule,
/// the hourly customer roll, and every work press (GarageOpsRules.DoWork).
/// </summary>
public static class GarageRules
{
    public const long Price = 100_000;

    public static bool IsOwned(GameData data) => data.HasFlag(StoryKeys.GarageDeed);

    /// <summary>
    /// Pure pre-purchase check — WorldSim.BuyGarage refuses on anything but Ok,
    /// strictly before it mutates. Owned wins over funds: a stamped deed means
    /// there is nothing left to buy, however rich the buyer.
    /// </summary>
    public static GarageSaleResult CanBuy(GameData data)
    {
        if (IsOwned(data))
        {
            return GarageSaleResult.AlreadyOwned;
        }
        if (data.Player.Money < Price)
        {
            return GarageSaleResult.InsufficientFunds;
        }
        return GarageSaleResult.Ok;
    }
}
