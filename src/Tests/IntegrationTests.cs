using Godot;
using TheHaunt.Core;
using TheHaunt.Player;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class IntegrationTests
{
    // Bed Area2D position in TestMap: footprint center of tiles (8,8)-(8,9). See spec §3.
    private static readonly Vector2 BedPosition = new(136, 152);

    [SimTest]
    public static async Task Events_MapSwapStress(TestContext t)
    {
        // Catches leaked C# event subscriptions on freed nodes: any handler still wired
        // to the clock after Free() throws when the next tick fires.
        try
        {
            Clock.Instance.SetTime(new GameTime(0));
            for (int i = 0; i < 50; i++)
            {
                var map = new TestMap();
                t.Host.AddChild(map);
                await t.WaitFrames(1);
                Clock.Instance.AdvanceMinutes(10);
                map.Free();
                await t.WaitFrames(1);
            }
            t.AssertEqual(500L, Clock.Instance.Now.TotalMinutes, "clock advanced through all 50 swaps");
        }
        finally
        {
            SaveService.Instance.NewGame();
        }
    }

    [SimTest]
    public static async Task Integration_MainBootAndSleep(TestContext t)
    {
        Node? main = null;
        try
        {
            var packed = GD.Load<PackedScene>("res://scenes/Main.tscn");
            main = packed.Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);

            t.Assert(main.GetNodeOrNull<Node2D>("World/Player") != null, "World/Player exists after boot");

            long dayBefore = Clock.Instance.Now.DayIndex;
            GameState.Instance.TransitionTo(GameState.Phase.Sleeping);

            bool completed = await t.WaitUntil(
                () => Clock.Instance.Now.DayIndex > dayBefore
                    && GameState.Instance.Current == GameState.Phase.Playing,
                10);
            t.Assert(completed, "sleep flow advanced the day and returned to Playing within 10 s");
            t.Assert(SaveService.Instance.SaveFileExists(),
                $"autosave file exists for slot '{SaveService.DefaultSlot}'");

            // Prove the autosave round-trips from disk: reboot Main and let its boot
            // path Load the file — the loaded clock must match the post-sleep morning.
            long expectedMinutes = Clock.Instance.Now.TotalMinutes;
            main.Free();
            main = null;
            await t.WaitFrames(1);
            SaveService.Instance.NewGame(); // reset, so the reboot's Load visibly changes state
            main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);
            t.AssertEqual(expectedMinutes, Clock.Instance.Now.TotalMinutes,
                "rebooted Main loaded the autosave from disk");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    [SimTest]
    public static async Task Interaction_ProbeFindsBed(TestContext t)
    {
        Node? main = null;
        try
        {
            var packed = GD.Load<PackedScene>("res://scenes/Main.tscn");
            main = packed.Instantiate();
            t.Host.AddChild(main);
            await t.WaitFrames(5);

            // Guards against a leaked autosave from an earlier test coupling this boot.
            t.AssertEqual(0L, Clock.Instance.Now.TotalMinutes, "boots fresh (no leaked autosave)");

            var maybePlayer = main.GetNodeOrNull<PlayerController>("World/Player");
            t.Assert(maybePlayer != null, "World/Player exists after boot");
            PlayerController player = maybePlayer!;

            // Stand just below the bed and face up so the probe reaches into it.
            player.GlobalPosition = BedPosition + new Vector2(0, 28);
            player.Probe.SetFacing(3);

            bool focused = await t.WaitUntil(() => player.Probe.Focused != null, 2);
            t.Assert(focused, "probe focused an interactable near the bed within 2 s");
            t.AssertEqual("Sleep", player.Probe.Focused!.PromptText, "focused prompt text");
        }
        finally
        {
            await CleanupMainAsync(t, main);
        }
    }

    // Free the Main instance first so its event subscriptions are gone, then restore
    // global phase, save data, and clock time for the next test.
    private static async Task CleanupMainAsync(TestContext t, Node? main)
    {
        if (main != null && GodotObject.IsInstanceValid(main))
        {
            main.Free();
        }
        await t.WaitFrames(1);
        GameState.Instance.TransitionTo(GameState.Phase.Playing);
        SaveService.Instance.NewGame();

        // Delete the default-slot autosave so every Main boot starts from a known
        // no-save state — otherwise one test's autosave couples into the next boot.
        string path = Path.Combine(SaveService.SaveDirectory, SaveService.DefaultSlot + ".json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        if (File.Exists(path + ".tmp"))
        {
            File.Delete(path + ".tmp");
        }
    }
}
