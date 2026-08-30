using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class SkillTests
{
    [SimTest]
    public static void Skill_CurveAndRosterPins(TestContext t)
    {
        // Kevin's v1 numbers, pinned so a rebalance is a decision, not a drift:
        // four skills, 1-10, 10 XP per level, 1 XP per practice.
        t.AssertEqual(4, SkillIds.All.Count, "four skills");
        t.AssertEqual(SkillIds.Farming, SkillIds.All[0], "roster order: farming");
        t.AssertEqual(SkillIds.MechanicalRepair, SkillIds.All[1], "roster order: mechanical repair");
        t.AssertEqual(SkillIds.Foraging, SkillIds.All[2], "roster order: foraging");
        t.AssertEqual(SkillIds.Combat, SkillIds.All[3], "roster order: combat");
        t.AssertEqual(10, SkillRules.MaxLevel, "levels run 1-10");
        t.AssertEqual(10, SkillRules.XpPerLevel, "each level requires 10 XP");
        t.AssertEqual("Mechanical Repair", SkillIds.DisplayName(SkillIds.MechanicalRepair),
            "display name");
        t.AssertEqual("future_skill", SkillIds.DisplayName("future_skill"),
            "unknown ids echo, never throw — display code is safe against future-save ids");

        // The whole curve at its edges.
        t.AssertEqual(1, SkillRules.LevelForXp(0), "level 1 at zero XP");
        t.AssertEqual(1, SkillRules.LevelForXp(9), "level 1 through 9 XP");
        t.AssertEqual(2, SkillRules.LevelForXp(10), "level 2 exactly at 10");
        t.AssertEqual(9, SkillRules.LevelForXp(89), "level 9 through 89");
        t.AssertEqual(10, SkillRules.LevelForXp(90), "level 10 exactly at 90");
        t.AssertEqual(10, SkillRules.LevelForXp(9000), "the cap holds however far XP runs");
        t.AssertEqual(1, SkillRules.LevelForXp(-5), "hostile negatives read level 1");
        t.AssertEqual(3L, SkillRules.XpIntoLevel(43), "progress inside the level");

        GameData data = GameData.NewGame();
        t.AssertEqual(0L, SkillRules.Xp(data, SkillIds.Farming), "a new game banks nothing");
        t.AssertEqual(1, SkillRules.Level(data, SkillIds.Combat), "every skill starts at level 1");

        // AddXp reports the crossed edge and keeps counting past the cap.
        t.AssertEqual((1, 1), SkillRules.AddXp(data, SkillIds.Farming), "one practice, no edge");
        data.Player.SkillXp[SkillIds.Farming] = 9;
        t.AssertEqual((1, 2), SkillRules.AddXp(data, SkillIds.Farming), "the tenth point levels up");
        data.Player.SkillXp[SkillIds.Farming] = 95;
        t.AssertEqual((10, 10), SkillRules.AddXp(data, SkillIds.Farming), "practice at the cap still banks");
        t.AssertEqual(96L, SkillRules.Xp(data, SkillIds.Farming), "raw XP keeps accumulating past 90");

        // Non-positive amounts are a refused no-op — XP is monotone like flags.
        t.AssertEqual((10, 10), SkillRules.AddXp(data, SkillIds.Farming, 0), "zero grant refused");
        t.AssertEqual((10, 10), SkillRules.AddXp(data, SkillIds.Farming, -3), "negative grant refused");
        t.AssertEqual(96L, SkillRules.Xp(data, SkillIds.Farming), "refusals bank nothing");
    }

    [SimTest]
    public static async Task Skill_HarvestGrantsFarmingXp(TestContext t)
    {
        // The one live XP source (Kevin: "any harvested crop is 1 point"), observed
        // at the bus. A level edge fires SkillsChanged then SkillLeveledUp, in that
        // order; a plain grant fires only SkillsChanged.
        SaveService service = SaveService.Instance;
        TestMap? map = null;
        var sequence = new List<string>();
        void OnSkills() => sequence.Add("skills");
        void OnLevel(string id, int level) => sequence.Add($"level:{id}:{level}");
        WorldSim.Instance.SkillsChanged += OnSkills;
        WorldSim.Instance.SkillLeveledUp += OnLevel;
        try
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            GameData data = service.Current;
            map = new TestMap { MapId = MapIds.Farm };
            t.Host.AddChild(map);
            await t.WaitFrames(1);

            // A mature turnip planted directly in the model; empty hands harvest it.
            MapState farm = data.GetMap(MapIds.Farm);
            int total = CropDefs.Get("turnip").TotalDays;
            farm.SetTile(new TileRecord
            {
                X = 20, Y = 14, Kind = "tilled", CropId = "turnip", GrowthDay = total,
            });
            data.Player.SkillXp[SkillIds.Farming] = 9;   // one practice from the edge

            t.AssertEqual(ActionOutcome.Harvested,
                WorldSim.Instance.UseSelectedItem(new Vector2I(20, 14)), "the harvest lands");
            t.AssertEqual(10L, SkillRules.Xp(data, SkillIds.Farming), "one harvest, one point");
            t.AssertEqual(2, SkillRules.Level(data, SkillIds.Farming), "the tenth point leveled up");
            t.AssertEqual("skills|level:farming:2", string.Join("|", sequence),
                "SkillsChanged before SkillLeveledUp");

            // A second mature crop: plain grant, no edge, no level event.
            sequence.Clear();
            farm.SetTile(new TileRecord
            {
                X = 21, Y = 14, Kind = "tilled", CropId = "turnip", GrowthDay = total,
            });
            t.AssertEqual(ActionOutcome.Harvested,
                WorldSim.Instance.UseSelectedItem(new Vector2I(21, 14)), "second harvest");
            t.AssertEqual("skills", string.Join("|", sequence), "no edge, no level event");

            // The failed path grants nothing: an empty tile is InvalidTarget.
            sequence.Clear();
            WorldSim.Instance.UseSelectedItem(new Vector2I(22, 14));
            t.AssertEqual("", string.Join("|", sequence), "a non-harvest grants no XP");
            t.AssertEqual(11L, SkillRules.Xp(data, SkillIds.Farming), "XP unchanged");
        }
        finally
        {
            WorldSim.Instance.SkillsChanged -= OnSkills;
            WorldSim.Instance.SkillLeveledUp -= OnLevel;
            if (map != null)
            {
                map.Free();
                await t.WaitFrames(1);
            }
            service.NewGame();
        }
    }

    [SimTest]
    public static void Skill_XpSurvivesTheSaveFile(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            service.NewGame();
            GameData data = service.Current;
            data.Player.SkillXp[SkillIds.Farming] = 37;
            data.Player.SkillXp["future_skill"] = 5;   // unknown ids ride along

            service.DeserializeFrom(service.SerializeToString());
            GameData loaded = service.Current;
            t.AssertEqual(37L, SkillRules.Xp(loaded, SkillIds.Farming), "XP round-trips");
            t.AssertEqual(4, SkillRules.Level(loaded, SkillIds.Farming), "level re-derives");
            t.AssertEqual(5L, loaded.Player.SkillXp["future_skill"],
                "unknown skill ids are preserved verbatim (flags rule)");

            // Load repair: negative XP clamps to zero, the degenerate key drops.
            service.DeserializeFrom("""
                {"SaveVersion":7,"TotalMinutes":0,
                 "Player":{"SkillXp":{"farming":-9,"combat":12,"":3}}}
                """);
            loaded = service.Current;
            t.AssertEqual(0L, SkillRules.Xp(loaded, SkillIds.Farming), "negative XP clamps to 0");
            t.AssertEqual(12L, SkillRules.Xp(loaded, SkillIds.Combat), "well-formed XP kept");
            t.Assert(!loaded.Player.SkillXp.ContainsKey(""), "degenerate key dropped");
        }
        finally
        {
            service.NewGame();
        }
    }
}
