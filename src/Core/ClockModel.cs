namespace TheHaunt.Core;

public sealed class ClockModel
{
    private double _acc;

    public GameTime Now { get; private set; } = new(0);
    public double SecondsPerGameMinute { get; set; } = 0.7; // Stardew pace
    public double TimeScale { get; set; } = 1.0;
    public int MaxMinutesPerFrame { get; set; } = 5;
    public bool AtEndOfDay => Now.MinuteOfDay >= GameTime.MinutesPerDay - 1;

    public event Action<GameTime>? MinuteTicked;      // display-only consumers (HUD)
    public event Action<GameTime>? TenMinuteTicked;   // canonical sim hook
    public event Action<GameTime>? HourTicked;
    public event Action<GameTime>? DayEnded;          // payload: time before the day advance
    public event Action<GameTime>? DayStarted;        // payload: new day's 6:00 time

    public void Accumulate(double deltaSeconds)
    {
        _acc += deltaSeconds * TimeScale;
        int steps = 0;
        while (_acc >= SecondsPerGameMinute && steps < MaxMinutesPerFrame)
        {
            if (AtEndOfDay)
            {
                _acc = 0;
                return;
            }
            _acc -= SecondsPerGameMinute;
            StepOneMinute();
            steps++;
        }
        // Discard runaway backlog (e.g. after a long hitch) so we never batch a huge catch-up.
        if (_acc > SecondsPerGameMinute)
        {
            _acc = SecondsPerGameMinute;
        }
    }

    // Deterministic test/dev seam: steps minute-by-minute with no per-frame cap.
    public void AdvanceMinutes(int minutes)
    {
        for (int i = 0; i < minutes; i++)
        {
            if (AtEndOfDay)
            {
                return;
            }
            StepOneMinute();
        }
    }

    // The ONLY way the clock crosses a day boundary. Fires no minute/ten-minute/hour ticks.
    public void AdvanceToDayStart()
    {
        DayEnded?.Invoke(Now);
        Now = GameTime.StartOfDay(Now.DayIndex + 1);
        _acc = 0;
        DayStarted?.Invoke(Now);
    }

    // Load path: sets Now, clears the accumulator, fires nothing.
    public void SetTime(GameTime time)
    {
        Now = time;
        _acc = 0;
    }

    private void StepOneMinute()
    {
        Now = Now.AddMinutes(1);
        MinuteTicked?.Invoke(Now);
        if (Now.MinuteOfDay % 10 == 0)
        {
            TenMinuteTicked?.Invoke(Now);
        }
        if (Now.MinuteOfDay % 60 == 0)
        {
            HourTicked?.Invoke(Now);
        }
    }
}
