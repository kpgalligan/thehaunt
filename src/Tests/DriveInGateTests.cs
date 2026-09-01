using Godot;
using TheHaunt.Content;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class DriveInGateTests
{
    /// <summary>
    /// The drive-in's entry chain (Kevin, 2026-09-01 / D4): up outside summer
    /// trading hours — barrier drawn, cells blocked, south exit disabled — and down
    /// within them. The map polls the clock, so a season/hour change flips the
    /// chain with no repaint call. The arrival spawn from the theater sits NORTH of
    /// the chain row, so leaving after close is always possible.
    /// </summary>
    [SimTest]
    public static async Task DriveIn_ChainGatesTheDrive(TestContext t)
    {
        SaveService service = SaveService.Instance;
        EastForkMap? map = null;
        try
        {
            service.NewGame();                          // day 0 — Spring
            Clock.Instance.SetTime(new GameTime(300));  // 11:00 AM
            map = new EastForkMap();
            t.Host.AddChild(map);
            await t.WaitFrames(2);                      // _Ready + one poll
            map.ApplyState(service.Current.GetMap(MapIds.EastFork));

            t.Assert(!DriveIn.ChainDown(Clock.Instance.Now), "spring morning: chain up");
            t.Assert(!map.IsStandable(new Vector2I(33, 27)), "chain cell blocked (west)");
            t.Assert(!map.IsStandable(new Vector2I(34, 27)), "chain cell blocked (east)");
            var exit = map.GetNodeOrNull<MapExit>("SouthExit");
            t.Assert(exit != null, "the south exit exists");
            t.Assert(exit!.IsEnabled != null && !exit.IsEnabled(),
                "south exit disabled while chained");
            var chain = map.GetNodeOrNull<Node2D>("TheaterChain");
            t.Assert(chain is { Visible: true }, "the chain is drawn while up");

            // The way back out from inside never closes: the arrival spawn's tile
            // stays standable with the chain up.
            t.Assert(map.IsStandable(new Vector2I(33, 26)), "arrival tile north of the chain is open");

            // Summer, inside the window: the next poll drops the chain.
            long summerDay = (long)Season.Summer * GameTime.DaysPerSeason + 2;
            Clock.Instance.SetTime(new GameTime(summerDay * GameTime.MinutesPerDay + 300));
            await t.WaitFrames(2);
            t.Assert(DriveIn.ChainDown(Clock.Instance.Now), "summer 11 AM: chain down");
            t.Assert(map.IsStandable(new Vector2I(33, 27)), "chain cell open (west)");
            t.Assert(map.IsStandable(new Vector2I(34, 27)), "chain cell open (east)");
            t.Assert(exit.IsEnabled!(), "south exit enabled");
            t.Assert(chain is { Visible: false }, "the chain is not drawn while down");

            // 6 PM sharp: back up.
            Clock.Instance.SetTime(new GameTime(summerDay * GameTime.MinutesPerDay + 720));
            await t.WaitFrames(2);
            t.Assert(!map.IsStandable(new Vector2I(33, 27)), "chain back up at close");
            t.Assert(!exit.IsEnabled!(), "south exit disabled again at close");
        }
        finally
        {
            if (map != null && GodotObject.IsInstanceValid(map))
            {
                map.Free();
            }
            await t.WaitFrames(1);
            Clock.Instance.SetTime(new GameTime(0));
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }
}
