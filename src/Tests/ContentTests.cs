using System.Text.RegularExpressions;
using TheHaunt.Content;
using TheHaunt.Core;

namespace TheHaunt.Tests;

/// <summary>
/// Validation over the content layer — the tests that replaced the lore markdown as
/// the mechanism keeping "the world" and "the game" the same thing
/// (docs/content-spec.md §7.2). Pure model reads; no scene tree.
/// </summary>
public static class ContentTests
{
    [SimTest]
    public static void Content_CharactersRegisterOnce(TestContext t)
    {
        var ids = new HashSet<string>();
        var blocks = new HashSet<(string Sheet, int Block)>();
        int dialogueCount = 0;
        foreach (CharacterDef character in Characters.All)
        {
            t.Assert(ids.Add(character.Npc.Id), $"'{character.Npc.Id}' registers once");
            t.Assert(blocks.Add((character.Npc.SpriteSheet, character.Npc.SpriteBlock)),
                $"'{character.Npc.Id}' has its own atlas block");
            dialogueCount += character.Dialogues.Count;
        }
        // Every authored def is in the derived registry exactly once (ToDictionary
        // would have thrown on a duplicate id at type-init).
        t.AssertEqual(dialogueCount + IntroBeats.All.Count, DialogueDefs.All.Count,
            "characters' dialogues + the intro beats = the whole registry");
        t.AssertEqual(Characters.All.Count, NpcDefs.All.Count,
            "one NpcDef per character");
    }

    [SimTest]
    public static void Content_EveryoneTalksOrIsSilentByDesign(TestContext t)
    {
        // Over the sampled grid, a silent-by-design character never talks, everyone
        // else talks somewhere, and every id a Talk selector hands out resolves.
        string[] introFlags =
        {
            StoryKeys.FirstPlanting, StoryKeys.RoadCleared,
            StoryKeys.CrewArrivalDone, StoryKeys.Overslept, StoryKeys.MeetingDone,
        };
        int[] minutes = { 0, 479, 480, 659, 660, 1199 };

        foreach (CharacterDef character in Characters.All)
        {
            bool everTalks = false;
            for (int combo = 0; combo < 32; combo++)
            {
                foreach (bool openJob in new[] { false, true })
                {
                    var data = new GameData();
                    for (int bit = 0; bit < introFlags.Length; bit++)
                    {
                        if ((combo & (1 << bit)) != 0)
                        {
                            data.TrySetFlag(introFlags[bit], 1);
                        }
                    }
                    if (openJob)
                    {
                        data.GarageJobs.Add(new GarageJobRecord { ServiceId = "oil_change" });
                    }
                    foreach (long day in new[] { 2L, 3L })
                    {
                        foreach (int minute in minutes)
                        {
                            var now = new GameTime(day * GameTime.MinutesPerDay + minute);
                            string? id = character.Talk(data, now);
                            if (id == null)
                            {
                                continue;
                            }
                            everTalks = true;
                            t.Assert(DialogueDefs.TryGet(id) != null,
                                $"'{character.Npc.Id}' hands out '{id}', which resolves");
                        }
                    }
                }
            }
            t.AssertEqual(!character.SilentByDesign, everTalks,
                $"'{character.Npc.Id}' talks somewhere iff not silent-by-design");
        }
    }

    // Canon writing rule (Kevin, 2026-09-01 / D9): the writing uses no pronouns for
    // Sam, ever. Sam's own lines may speak of others; a line ABOUT Sam may not
    // gender Sam.
    [SimTest]
    public static void Content_SamIsNeverGendered(TestContext t)
    {
        var pronoun = new Regex(@"\b(he|she|him|her|his|hers)\b", RegexOptions.IgnoreCase);
        int checkedLines = 0;
        foreach (DialogueDef def in DialogueDefs.All.Values)
        {
            foreach (DialogueNode node in def.Nodes.Values)
            {
                foreach (DialogueLine line in node.Lines)
                {
                    checkedLines++;
                    if (line.SpeakerRole == Sam.Id || !line.Text.Contains("Sam", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    t.Assert(!pronoun.IsMatch(line.Text),
                        $"dialogue '{def.Id}': a line about Sam uses a pronoun: \"{line.Text}\"");
                }
            }
        }
        foreach (LetterDef letter in LetterDefs.All.Values)
        {
            if (letter.Body.Contains("Sam", StringComparison.Ordinal))
            {
                t.Assert(!pronoun.IsMatch(letter.Body),
                    $"letter '{letter.Id}' mentions Sam and uses a pronoun");
            }
        }
        t.Assert(checkedLines > 50, $"the sweep actually read the copy ({checkedLines} lines)");
    }

    // Canon writing rule (Kevin, 2026-09-01 / D9): nobody talks about the pit. The
    // sign may say DANGER; the people never say pit — in dialogue, choices, letters,
    // or quest copy.
    [SimTest]
    public static void Content_ActOneNeverNamesThePit(TestContext t)
    {
        var pit = new Regex(@"\bpit\b", RegexOptions.IgnoreCase);
        foreach (DialogueDef def in DialogueDefs.All.Values)
        {
            foreach (DialogueNode node in def.Nodes.Values)
            {
                foreach (DialogueLine line in node.Lines)
                {
                    t.Assert(!pit.IsMatch(line.Text),
                        $"dialogue '{def.Id}' names the pit: \"{line.Text}\"");
                }
                foreach (DialogueChoice choice in node.Choices ?? (IReadOnlyList<DialogueChoice>)Array.Empty<DialogueChoice>())
                {
                    t.Assert(!pit.IsMatch(choice.Text),
                        $"dialogue '{def.Id}' choice names the pit: \"{choice.Text}\"");
                }
            }
        }
        foreach (LetterDef letter in LetterDefs.All.Values)
        {
            t.Assert(!pit.IsMatch(letter.Title) && !pit.IsMatch(letter.Body),
                $"letter '{letter.Id}' names the pit");
        }
        foreach (QuestDef quest in QuestDefs.All.Values)
        {
            t.Assert(!pit.IsMatch(quest.Title) && !pit.IsMatch(quest.Description),
                $"quest '{quest.Id}' names the pit");
        }
    }

    // Planned stubs stay out of the game until they ship (content-spec principle 4):
    // the drive-in arc reserves its id prefix and registers nothing under it.
    [SimTest]
    public static void Content_PlannedStubsAreUnregistered(TestContext t)
    {
        t.AssertEqual("drive_in.", DriveInArc.IdPrefix, "the arc's reserved prefix");
        foreach (string id in QuestDefs.All.Keys)
        {
            t.Assert(!id.StartsWith(DriveInArc.IdPrefix, StringComparison.Ordinal),
                $"quest '{id}' ships under the planned arc's prefix");
        }
        foreach (string id in LetterDefs.All.Keys)
        {
            t.Assert(!id.StartsWith(DriveInArc.IdPrefix, StringComparison.Ordinal),
                $"letter '{id}' ships under the planned arc's prefix");
        }
    }

    // The drive-in chain (Kevin, 2026-09-01 / D4): down only in summer, 9 AM - 6 PM
    // — and Shelly's presence is EXACTLY the chain-down window, because she is the
    // one who opens it (they share DriveIn's constants and the season gate, and
    // this pins that they can never diverge).
    [SimTest]
    public static void Content_DriveInChainMatchesShelly(TestContext t)
    {
        NpcDef shelly = NpcDefs.Get(Shelly.Id);
        var data = new GameData();
        int[] minutes = { 0, 179, 180, 400, 719, 720, 1199 };
        foreach (Season season in Enum.GetValues<Season>())
        {
            long day = (long)season * GameTime.DaysPerSeason + 3;
            foreach (int minute in minutes)
            {
                var now = new GameTime(day * GameTime.MinutesPerDay + minute);
                t.AssertEqual(season, now.Season, "the sampled day lands in its season");
                bool chainDown = DriveIn.ChainDown(now);
                t.AssertEqual(
                    season == Season.Summer && minute >= 180 && minute < 720,
                    chainDown,
                    $"chain state at {season} minute {minute}");
                NpcPlacement? placed = NpcSchedules.Resolve(shelly, data, now);
                t.AssertEqual(chainDown, placed != null,
                    $"Shelly present iff the chain is down ({season}, minute {minute})");
                if (placed != null)
                {
                    t.AssertEqual(MapIds.DriveIn, placed.Value.MapId, "Shelly reads at the drive-in");
                }
            }
        }
    }
}
