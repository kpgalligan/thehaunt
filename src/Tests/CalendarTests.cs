using TheHaunt.Core;

namespace TheHaunt.Tests;

public static class CalendarTests
{
    [SimTest]
    public static void Calendar_ClockStrings(TestContext t)
    {
        t.AssertEqual("6:00 AM", new GameTime(0).ToClockString(), "t=0");
        t.AssertEqual("12:00 PM", new GameTime(360).ToClockString(), "t=360");
        t.AssertEqual("12:00 AM", new GameTime(1080).ToClockString(), "t=1080");
        t.AssertEqual("1:00 AM", new GameTime(1140).ToClockString(), "t=1140");
        t.AssertEqual("1:59 AM", new GameTime(1199).ToClockString(), "t=1199");
    }

    [SimTest]
    public static void Calendar_Dates(TestContext t)
    {
        t.AssertEqual("Mon. Spring 1, Year 1", new GameTime(0).ToDateString(), "t=0 date");

        GameTime day27 = GameTime.StartOfDay(27);
        t.AssertEqual(Season.Spring, day27.Season, "day 27 season");
        t.AssertEqual(28, day27.DayOfSeason, "day 27 day-of-season");

        GameTime day28 = GameTime.StartOfDay(28);
        t.AssertEqual(Season.Summer, day28.Season, "day 28 season");
        t.AssertEqual(1, day28.DayOfSeason, "day 28 day-of-season");

        GameTime day111 = GameTime.StartOfDay(111);
        t.AssertEqual(Season.Winter, day111.Season, "day 111 season");
        t.AssertEqual(28, day111.DayOfSeason, "day 111 day-of-season");
        t.AssertEqual(1, day111.Year, "day 111 year");

        GameTime day112 = GameTime.StartOfDay(112);
        t.AssertEqual(Season.Spring, day112.Season, "day 112 season");
        t.AssertEqual(1, day112.DayOfSeason, "day 112 day-of-season");
        t.AssertEqual(2, day112.Year, "day 112 year");

        t.AssertEqual(0, GameTime.StartOfDay(7).WeekdayIndex, "day 7 weekday (Monday)");
    }
}
