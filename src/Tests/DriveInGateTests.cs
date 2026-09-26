using Godot;
using TheHaunt.Content;
using TheHaunt.Core;
using TheHaunt.Player;
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
    /// the chain row, so leaving after close is always possible — and a raise is
    /// HELD while the player's feet are on the chain's cells or the sign's, so a
    /// collider never lands on the player (the drive, exit included, stays open
    /// until the feet clear).
    /// </summary>
    [SimTest]
    public static async Task DriveIn_ChainGatesTheDrive(TestContext t)
    {
        SaveService service = SaveService.Instance;
        EastForkMap? map = null;
        PlayerController? player = null;
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
            var exit = map.GetNodeOrNull<MapExit>("Exit_" + MapIds.DriveIn);
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

            // 6 PM sharp with the player's feet on a chain cell: the raise is held —
            // the cell stays open and so does the exit — until the feet clear.
            player = new PlayerController();
            t.Host.AddChild(player);
            await t.WaitFrames(1);
            t.Assert(player.IsInGroup(MapRoot.PlayerGroup), "the player joins the player group");
            player.GlobalPosition = new Vector2(33 * 16 + 8, 27 * 16 + 2); // feet box inside (33,27)
            Clock.Instance.SetTime(new GameTime(summerDay * GameTime.MinutesPerDay + 720));
            await t.WaitFrames(2);
            t.Assert(!DriveIn.ChainDown(Clock.Instance.Now), "summer 6 PM: the clock says up");
            t.Assert(map.IsStandable(new Vector2I(33, 27)), "but the raise is held under the player");
            t.Assert(exit.IsEnabled!(), "and the exit stays open while the chain is still down");

            // The sign's cell counts too: its blocker returns with the chain.
            player.GlobalPosition = new Vector2(32 * 16 + 8, 26 * 16 + 2);
            await t.WaitFrames(2);
            t.Assert(map.IsStandable(new Vector2I(33, 27)), "held while the player stands on the sign's cell");

            // Feet clear: the next poll raises it.
            player.GlobalPosition = new Vector2(33 * 16 + 8, 24 * 16 + 8);
            await t.WaitFrames(2);
            t.Assert(!map.IsStandable(new Vector2I(33, 27)), "chain back up at close");
            t.Assert(!exit.IsEnabled!(), "south exit disabled again at close");
        }
        finally
        {
            if (player != null && GodotObject.IsInstanceValid(player))
            {
                player.Free();
            }
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
