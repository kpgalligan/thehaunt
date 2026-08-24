namespace TheHaunt.Core;

public readonly record struct GameTime(long TotalMinutes)
{
    public const int MinutesPerDay = 1200;   // a day runs 6:00 -> 26:00 (2 AM), monotonic past midnight
    public const int DayStartHour = 6;
    public const int DaysPerSeason = 28;
    public const int SeasonsPerYear = 4;
    public const int DaysPerYear = 112;

    private static readonly string[] WeekdayNames = { "Mon.", "Tue.", "Wed.", "Thu.", "Fri.", "Sat.", "Sun." };

    public long DayIndex => TotalMinutes / MinutesPerDay;
    public int MinuteOfDay => (int)(TotalMinutes % MinutesPerDay);
    public int AbsoluteHour => DayStartHour + MinuteOfDay / 60;   // 6..25 (24 = midnight, 25 = 1 AM)
    public int Minute => MinuteOfDay % 60;
    public Season Season => (Season)((DayIndex / DaysPerSeason) % SeasonsPerYear);
    public int DayOfSeason => (int)(DayIndex % DaysPerSeason) + 1;   // 1..28
    public int Year => (int)(DayIndex / DaysPerYear) + 1;            // 1-based
    public int WeekdayIndex => (int)(DayIndex % 7);                  // 0 = Monday

    public GameTime AddMinutes(long minutes) => new(TotalMinutes + minutes);

    public static GameTime StartOfDay(long dayIndex) => new(dayIndex * MinutesPerDay);

    public string ToClockString()
    {
        int h24 = AbsoluteHour % 24;
        string suffix = h24 < 12 ? "AM" : "PM";
        int h12 = h24 % 12;
        if (h12 == 0)
        {
            h12 = 12;
        }
        return $"{h12}:{Minute:00} {suffix}";
    }

    public string ToDateString() => $"{WeekdayNames[WeekdayIndex]} {Season} {DayOfSeason}, Year {Year}";
}
