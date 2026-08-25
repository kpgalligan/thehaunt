namespace TheHaunt.Core;

// Shop opening hours (minute-of-day; minute 0 = 6:00 AM). The shopkeeper's
// schedule references these constants directly so "shop open" and "shopkeeper
// present" can never diverge. Doors are never locked — only the counter closes.
public static class ShopHours
{
    public const int OpenMinute  = 180;   // 9:00 AM  [KEVIN]
    public const int CloseMinute = 660;   // 5:00 PM  [KEVIN]

    // Start-inclusive, end-exclusive (same convention as ScheduleEntry windows).
    public static bool IsOpen(int minuteOfDay)
        => minuteOfDay >= OpenMinute && minuteOfDay < CloseMinute;
}
