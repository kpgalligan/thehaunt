using TheHaunt.Core;

namespace TheHaunt.Tests;

public static class ClockTests
{
    [SimTest]
    public static void Clock_TickEventCadence(TestContext t)
    {
        var model = new ClockModel();
        int minuteTicks = 0;
        var tenMinuteAt = new List<int>();
        var hourAt = new List<int>();
        model.MinuteTicked += _ => minuteTicks++;
        model.TenMinuteTicked += time => tenMinuteAt.Add(time.MinuteOfDay);
        model.HourTicked += time => hourAt.Add(time.MinuteOfDay);

        model.AdvanceMinutes(61);

        t.AssertEqual(61, minuteTicks, "MinuteTicked count");
        t.AssertEqual(6, tenMinuteAt.Count, "TenMinuteTicked count");
        t.Assert(tenMinuteAt.SequenceEqual(new[] { 10, 20, 30, 40, 50, 60 }),
            $"TenMinuteTicked minutes-of-day: [{string.Join(",", tenMinuteAt)}]");
        t.AssertEqual(1, hourAt.Count, "HourTicked count");
        t.AssertEqual(60, hourAt[0], "HourTicked minute-of-day");
    }

    [SimTest]
    public static void Clock_EndOfDayClamp(TestContext t)
    {
        var model = new ClockModel();
        model.SetTime(new GameTime(1195));
        int minuteTicks = 0;
        int dayEvents = 0;
        model.MinuteTicked += _ => minuteTicks++;
        model.DayEnded += _ => dayEvents++;
        model.DayStarted += _ => dayEvents++;

        model.AdvanceMinutes(10);
        t.AssertEqual(1199, model.Now.MinuteOfDay, "clamped MinuteOfDay");
        t.AssertEqual(4, minuteTicks, "minute ticks up to the clamp");
        t.AssertEqual(0, dayEvents, "day events from ticking");

        minuteTicks = 0;
        model.AdvanceMinutes(5);
        t.AssertEqual(0, minuteTicks, "minute ticks past the clamp");
        t.AssertEqual(0, dayEvents, "day events past the clamp");
        t.AssertEqual(1199, model.Now.MinuteOfDay, "MinuteOfDay stays clamped");
    }

    [SimTest]
    public static void Clock_DayRollover(TestContext t)
    {
        // Reach a genuine clamped end of day before subscribing loggers.
        var model = new ClockModel();
        model.SetTime(new GameTime(1195));
        model.AdvanceMinutes(10);
        t.Assert(model.AtEndOfDay, "precondition: clamped at end of day");

        var log = new List<string>();
        long dayEndedTotal = -1;
        long dayStartedTotal = -1;
        model.MinuteTicked += _ => log.Add("Minute");
        model.TenMinuteTicked += _ => log.Add("TenMinute");
        model.HourTicked += _ => log.Add("Hour");
        model.DayEnded += time => { log.Add("DayEnded"); dayEndedTotal = time.TotalMinutes; };
        model.DayStarted += time => { log.Add("DayStarted"); dayStartedTotal = time.TotalMinutes; };

        long oldDay = model.Now.DayIndex;
        long oldTotal = model.Now.TotalMinutes;
        model.AdvanceToDayStart();

        t.Assert(log.SequenceEqual(new[] { "DayEnded", "DayStarted" }),
            $"event order: [{string.Join(",", log)}]");
        t.AssertEqual(oldTotal, dayEndedTotal, "DayEnded payload (time before advance)");
        t.AssertEqual(oldDay + 1, model.Now.DayIndex, "new DayIndex");
        t.AssertEqual(0, model.Now.MinuteOfDay, "new MinuteOfDay");
        t.AssertEqual(model.Now.TotalMinutes, dayStartedTotal, "DayStarted payload (new 6:00 time)");
    }

    [SimTest]
    public static void Clock_CatchupEquivalence(TestContext t)
    {
        var modelA = new ClockModel();
        var modelB = new ClockModel();
        List<string> logA = AttachLog(modelA);
        List<string> logB = AttachLog(modelB);

        modelA.AdvanceMinutes(50);
        for (int i = 0; i < 50; i++)
        {
            modelB.Accumulate(0.7);
        }

        t.AssertEqual(modelA.Now, modelB.Now, "Now after 50 minutes");
        t.Assert(logA.SequenceEqual(logB),
            $"event logs differ: A has {logA.Count} entries, B has {logB.Count}");

        // A single huge accumulation is capped at MaxMinutesPerFrame steps.
        var capped = new ClockModel();
        int steps = 0;
        capped.MinuteTicked += _ => steps++;
        capped.Accumulate(1000);
        t.AssertEqual(capped.MaxMinutesPerFrame, steps, "steps for one Accumulate(1000)");
    }

    private static List<string> AttachLog(ClockModel model)
    {
        var log = new List<string>();
        model.MinuteTicked += time => log.Add($"Minute@{time.TotalMinutes}");
        model.TenMinuteTicked += time => log.Add($"TenMinute@{time.TotalMinutes}");
        model.HourTicked += time => log.Add($"Hour@{time.TotalMinutes}");
        model.DayEnded += time => log.Add($"DayEnded@{time.TotalMinutes}");
        model.DayStarted += time => log.Add($"DayStarted@{time.TotalMinutes}");
        return log;
    }
}
