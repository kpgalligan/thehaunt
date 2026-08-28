using TheHaunt.Core;

namespace TheHaunt.Tests;

// The tool work loop's behaviour contract, pinned against the pure state machine:
// 90/90/140/90 ms frames, the mutation signal on ENTRY to the impact frame, and
// Kevin's amendment to the handoff — a press COMMITS one full cycle (a tap is one
// completed action; there is no release-before-impact cancel), holding repeats,
// and a held direction ends the loop at the cycle boundary. Steps use a 10 ms
// tick (finer than the 60 Hz physics that drives it).
public static class WorkAnimationTests
{
    private const float Tick = 0.01f;

    [SimTest]
    public static void Work_LoopTimingAndImpact(TestContext t)
    {
        var work = new WorkAnimation();
        work.Start();
        t.AssertEqual(0, work.Frame, "starts at the windup");

        int impacts = 0;
        float impactAt = -1f, elapsed = 0f;
        var frameAt = new Dictionary<int, int>(); // frame observed at key times
        while (elapsed < 0.395f)
        {
            WorkTick tick = work.Advance(Tick, held: true, moving: false);
            elapsed += Tick;
            if (tick.Impact && impacts++ == 0)
                impactAt = elapsed;
            t.Assert(!tick.Finished, "held loop never finishes");
            frameAt[(int)MathF.Round(elapsed * 1000)] = work.Frame;
        }
        t.AssertEqual(0, frameAt[80], "windup through 90ms");
        t.AssertEqual(1, frameAt[130], "strike through 180ms");
        t.AssertEqual(2, frameAt[250], "impact holds 140ms");
        t.AssertEqual(3, frameAt[350], "recover through 410ms");
        t.AssertEqual(1, impacts, "exactly one impact per cycle");
        t.Assert(impactAt > 0.17f && impactAt < 0.20f, $"impact fires entering frame 2 (at {impactAt:0.00}s)");

        // Still held: the loop wraps to the windup and impacts again next cycle.
        WorkTick wrap = work.Advance(0.02f, held: true, moving: false);
        t.Assert(work.Active && work.Frame == 0 && !wrap.Finished, "wraps to windup while held");
        var again = false;
        for (float s = 0; s < 0.2f; s += Tick)
            again |= work.Advance(Tick, held: true, moving: false).Impact;
        t.Assert(again, "second cycle impacts again");
    }

    [SimTest]
    public static void Work_TapCompletesTheSwing(TestContext t)
    {
        // A tap commits: released immediately after the press, the swing still
        // walks through strike, impact, and recover before ending.
        var work = new WorkAnimation();
        work.Start();
        int impacts = 0, lastFrame = 0;
        bool finished = false;
        float elapsed = 0f;
        while (elapsed < 0.60f && !finished)
        {
            WorkTick tick = work.Advance(Tick, held: false, moving: false);
            elapsed += Tick;
            if (tick.Impact)
                impacts++;
            lastFrame = Math.Max(lastFrame, work.Frame);
            finished = tick.Finished;
            if (!finished)
                t.Assert(work.Active, "the committed swing stays live to the end");
        }
        t.Assert(finished && !work.Active, "one full cycle, then done");
        t.AssertEqual(1, impacts, "the tapped swing lands its hit");
        t.AssertEqual(3, lastFrame, "the recover frame played out");
        t.Assert(elapsed > 0.40f && elapsed < 0.44f, $"full cycle length (~0.41s, got {elapsed:0.00}s)");
    }

    [SimTest]
    public static void Work_MovingEndsTheLoopAtTheCycleBoundary(TestContext t)
    {
        // A held direction never interrupts the committed cycle — it only stops
        // the loop from repeating once the recover frame has played out.
        var work = new WorkAnimation();
        work.Start();
        int impacts = 0;
        bool finished = false;
        float elapsed = 0f;
        while (elapsed < 0.60f && !finished)
        {
            WorkTick tick = work.Advance(Tick, held: true, moving: true);
            elapsed += Tick;
            if (tick.Impact)
                impacts++;
            finished = tick.Finished;
        }
        t.Assert(finished && !work.Active, "loop ends at the cycle boundary");
        t.AssertEqual(1, impacts, "the committed swing still lands its hit");
    }

    [SimTest]
    public static void Work_CancelDropsThePose(TestContext t)
    {
        // Cancel is the ONE way to drop a live cycle — the owner losing control
        // mid-swing (a story beat, sleep). No impact ever fires from it.
        var work = new WorkAnimation();
        work.Start();
        for (float s = 0; s < 0.05f; s += Tick)
            work.Advance(Tick, held: true, moving: false);
        work.Cancel();
        t.Assert(!work.Active && work.Frame == 0, "cancel resets the loop");
        WorkTick tick = work.Advance(Tick, held: true, moving: false);
        t.Assert(!tick.Impact && !tick.Finished && !work.Active, "a cancelled loop stays inert");
    }
}
