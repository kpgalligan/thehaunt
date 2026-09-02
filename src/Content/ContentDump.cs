using System.Reflection;
using System.Text;
using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Renders the whole content layer to one markdown document — the generated
/// "read the world" review view. GENERATED output: write it
/// to a scratch path via Main's <c>--dump-content</c> dev flag, read it, throw it
/// away. Never commit it and never edit it — the registries are the truth it is
/// printed from, and a maintained copy would be the parallel-prose problem again.
/// Pure and deterministic; needs no save and no scene tree.
/// </summary>
public static class ContentDump
{
    public static string Render()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# The Haunt — content dump");
        sb.AppendLine();
        sb.AppendLine("> GENERATED from `src/Content` by `ContentDump.Render()` — do not edit, do not commit.");
        sb.AppendLine("> Review queues: proposed = `rg \"\\[KEVIN\\]\" src` · planned = `rg PLANNED src/Content`.");
        sb.AppendLine();
        CastSection(sb);
        PlacesSection(sb);
        StorySection(sb);
        DialogueSection(sb);
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Characters
    // ------------------------------------------------------------------

    private static void CastSection(StringBuilder sb)
    {
        sb.AppendLine("## Characters");
        sb.AppendLine();
        sb.AppendLine($"Jane (`{Jane.Id}`) — the player; sheet `assets/sprites/character.png`; backstory in `Characters/Jane.cs`.");
        sb.AppendLine();
        foreach (CharacterDef c in Characters.All)
        {
            NpcDef npc = c.Npc;
            string sheet = npc.SpriteSheet[(npc.SpriteSheet.LastIndexOf('/') + 1)..];
            sb.AppendLine($"### {npc.DisplayRole} (`{npc.Id}`) — {sheet} block {npc.SpriteBlock}"
                + (c.SilentByDesign ? " · silent by design" : ""));
            sb.AppendLine();
            sb.AppendLine("Schedule (first match wins):");
            foreach (ScheduleEntry e in npc.Schedule)
            {
                sb.AppendLine($"- {Gates(e)}{Window(e.StartMinuteOfDay, e.EndMinuteOfDay)}"
                    + $" @ {e.Placement.MapId} ({e.Placement.TileX},{e.Placement.TileY}) f{e.Placement.Facing}"
                    + (e.Placement.Ambit > 0 ? $", ambit {e.Placement.Ambit}" : ", fixture"));
            }
            sb.AppendLine();
            if (!c.SilentByDesign)
            {
                TalkTable(sb, c);
            }
            if (c.Dialogues.Count > 0)
            {
                sb.AppendLine("Dialogues:");
                foreach (DialogueDef d in c.Dialogues)
                {
                    sb.AppendLine($"- `{d.Id}` ({d.Nodes.Count} node{(d.Nodes.Count == 1 ? "" : "s")}) — \"{FirstLine(d)}\"");
                }
                sb.AppendLine();
            }
        }
    }

    private static void TalkTable(StringBuilder sb, CharacterDef c)
    {
        (string Label, string[] Flags)[] states =
        {
            ("before the crew", Array.Empty<string>()),
            ("crew met", new[] { StoryKeys.CrewArrivalDone }),
            ("meeting done", new[] { StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone }),
        };
        int[] minutes = { 240, 540, 840 };   // 10 AM / 3 PM / 8 PM
        sb.AppendLine("Talk (sampled; `a / b` = even/odd day; `—` = silent):");
        sb.AppendLine("| state | 10 AM | 3 PM | 8 PM |");
        sb.AppendLine("|---|---|---|---|");
        foreach ((string label, string[] flags) in states)
        {
            sb.AppendLine(TalkRow(c, label, flags, withJob: false, minutes));
        }
        if (c.Npc.Id == Mike.Id)
        {
            sb.AppendLine(TalkRow(c, "…and a car waiting",
                new[] { StoryKeys.CrewArrivalDone, StoryKeys.MeetingDone }, withJob: true, minutes));
        }
        sb.AppendLine();
    }

    private static string TalkRow(CharacterDef c, string label, string[] flags, bool withJob, int[] minutes)
    {
        var row = new StringBuilder($"| {label} |");
        foreach (int minute in minutes)
        {
            string even = TalkAt(c, flags, withJob, day: 2, minute);
            string odd = TalkAt(c, flags, withJob, day: 3, minute);
            row.Append(' ').Append(even == odd ? even : $"{even} / {odd}").Append(" |");
        }
        return row.ToString();
    }

    private static string TalkAt(CharacterDef c, string[] flags, bool withJob, long day, int minute)
    {
        var data = new GameData();
        foreach (string flag in flags)
        {
            data.TrySetFlag(flag, 1);
        }
        if (withJob)
        {
            data.GarageJobs.Add(new GarageJobRecord { ServiceId = GarageServices.OilChange });
        }
        return c.Talk(data, new GameTime(day * GameTime.MinutesPerDay + minute)) ?? "—";
    }

    private static string FirstLine(DialogueDef d)
    {
        string text = d.Nodes[d.StartNodeId].Lines[0].Text;
        return text.Length > 70 ? text[..67] + "…" : text;
    }

    // ------------------------------------------------------------------
    // Places
    // ------------------------------------------------------------------

    private static void PlacesSection(StringBuilder sb)
    {
        sb.AppendLine("## Places");
        sb.AppendLine();
        var fresh = new GameData();
        Place(sb, "The farm", typeof(Farm),
            $"map `{Farm.MapId}` — the starter kit waits in the barn chest; recipe signs resolve their words here (`Farm.SignTextFor`)");
        Place(sb, "The barn", typeof(BarnRules),
            $"three drawn states: derelict → weathertight (`{StoryKeys.BarnWeathertight}`) → restored (`{StoryKeys.BarnRestored}`); nothing advances them yet");
        Place(sb, "West entry — the motel", typeof(MotelRules),
            $"rooms 1–{MotelRules.Rooms}, each behind its own flag (`{StoryKeys.MotelRoom1Open}` …); lit window + parked sedan: room {MotelRules.LitRoom(fresh)} (Pell); the NO circuit waits on `{StoryKeys.MotelFull}`");
        Place(sb, "West entry — the gas station", typeof(GasStation),
            $"staffed {Window(GasStation.OpenMinute, GasStation.CloseMinute)} — Dennis and the OPEN neon share the constants");
        Place(sb, "West entry — the fireworks stand", typeof(FireworksStand),
            "Gloria trades general-store hours; the catalog is PLANNED");
        Place(sb, "West entry — the garage", typeof(Garage),
            $"FOR SALE at {GarageRules.Price}g until `{StoryKeys.GarageDeed}`; open {Window(GarageOpsRules.OpenMinuteOfDay, GarageOpsRules.CloseMinuteOfDay)}, {GarageOpsRules.ArrivalPercent}% arrival roll per open hour, {GarageOpsRules.MaxCars} lifts; services: "
            + string.Join(", ", GarageServices.All.Select(s => $"{s.Name} {s.Price}g")));
        Place(sb, "Billie's — and the pit", typeof(Billies),
            "the bar runs 10:00 AM–close (the shifts are the characters' schedules); the pit is covered, chained, and unspoken");
        Place(sb, "The fork", typeof(Fork), "all road; the south stub is chained (deferred)");
        Place(sb, "Town centre", typeof(Town),
            $"built: the town hall and the general store (open {Window(ShopHours.OpenMinute, ShopHours.CloseMinute)}; shelf: "
            + string.Join(", ", ShopCatalog.All[ShopCatalog.GeneralStore].Select(e => $"{ItemDefs.Get(e.ItemId).Name} {e.BuyPrice}g"))
            + "); the rest of the centre is PLANNED");
        Place(sb, "East fork", typeof(EastFork),
            "Abe's shack; the mansion drive chained (mansion PLANNED); the theater drive south");
        Place(sb, "The drive-in", typeof(DriveIn),
            $"chain down {Window(DriveIn.OpenMinute, DriveIn.CloseMinute)} in summer only — exactly Shelly's window, because she opens it; the restoration arc is PLANNED (`{DriveInArc.IdPrefix}*` reserved)");
        Place(sb, "East entry", typeof(EastEntry),
            "the police station and the hardware store are shut (police PLANNED; the hardware owner's hospital stay goes unmentioned); Sam's salon keeps store hours");
    }

    private static void Place(StringBuilder sb, string title, Type place, string facts)
    {
        sb.AppendLine($"### {title}");
        sb.AppendLine();
        sb.AppendLine(facts + ".");
        var copy = Places.CopyOf(place).ToList();
        if (copy.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Copy:");
            foreach ((string name, string value) in copy)
            {
                sb.AppendLine($"- {name}: \"{value}\"");
            }
        }
        sb.AppendLine();
    }

    // ------------------------------------------------------------------
    // Story
    // ------------------------------------------------------------------

    private static void StorySection(StringBuilder sb)
    {
        sb.AppendLine("## Story");
        sb.AppendLine();
        sb.AppendLine("### Quests");
        sb.AppendLine();
        foreach (QuestDef quest in QuestDefs.All.Values)
        {
            sb.AppendLine($"- **{quest.Title}** (`{quest.Id}`) — {quest.Description}"
                + $" Starts on `{quest.StartFlag}`; completes on `{quest.CompleteFlag}`.");
        }
        sb.AppendLine();
        sb.AppendLine("### Letters");
        sb.AppendLine();
        foreach (LetterDef letter in LetterDefs.All.Values)
        {
            string gates = letter.RequiresFlag == null && letter.FromDay == 0
                ? "delivered from day 0"
                : $"delivered{(letter.RequiresFlag != null ? $" on `{letter.RequiresFlag}`" : "")}{(letter.FromDay > 0 ? $" from day {letter.FromDay}" : "")}";
            string package = letter.Items is { Count: > 0 }
                ? $"; package: {string.Join(", ", letter.Items.Select(i => $"{i.ItemId} x{i.Count}"))} under `{letter.TakenFlag}`"
                : "";
            sb.AppendLine($"- **{letter.Title}** (`{letter.Id}`) — {gates}; read stamps `{letter.ReadFlag}`{package}.");
            foreach (string line in letter.Body.Split('\n'))
            {
                sb.AppendLine($"  > {line}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("### Beats (StoryDirector)");
        sb.AppendLine();
        foreach (DialogueDef beat in IntroBeats.All)
        {
            sb.AppendLine($"- `{beat.Id}` — {beat.Nodes.Count} nodes, opens at `{beat.StartNodeId}`");
        }
        sb.AppendLine();
        sb.AppendLine("### Story flags (StoryKeys — value is the day stamped; absence = false)");
        sb.AppendLine();
        foreach (FieldInfo field in typeof(StoryKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)))
        {
            sb.AppendLine($"- `{field.GetRawConstantValue()}` ({field.Name})");
        }
        sb.AppendLine();
        sb.AppendLine("### Skills");
        sb.AppendLine();
        sb.AppendLine("- " + string.Join(" · ", SkillIds.All.Select(SkillIds.DisplayName))
            + $" — levels 1–{SkillRules.MaxLevel}, {SkillRules.XpPerLevel} XP each");
        sb.AppendLine();
    }

    // ------------------------------------------------------------------
    // Dialogue — every line in the game
    // ------------------------------------------------------------------

    private static void DialogueSection(StringBuilder sb)
    {
        sb.AppendLine("## Dialogue — every line in the game");
        sb.AppendLine();
        foreach (DialogueDef def in DialogueDefs.All.Values)
        {
            sb.AppendLine($"### `{def.Id}`");
            sb.AppendLine();
            foreach (DialogueNode node in def.Nodes.Values)
            {
                string arrow = node.NextNodeId != null ? $" → {node.NextNodeId}" : "";
                string stamps = node.SetsFlag != null ? $" [sets `{node.SetsFlag}`]" : "";
                sb.AppendLine($"**{node.Id}**{arrow}{stamps}");
                foreach (DialogueLine line in node.Lines)
                {
                    string speaker = line.SpeakerRole.Length == 0 ? "(narration)" : line.SpeakerRole;
                    sb.AppendLine($"- {speaker}: \"{line.Text}\"");
                }
                if (node.Choices is { Count: > 0 })
                {
                    foreach (DialogueChoice choice in node.Choices)
                    {
                        string choiceStamps = choice.SetsFlag != null ? $" [sets `{choice.SetsFlag}`]" : "";
                        sb.AppendLine($"- ▸ \"{choice.Text}\" → {choice.NextNodeId}{choiceStamps}");
                    }
                }
                sb.AppendLine();
            }
        }
    }

    // ------------------------------------------------------------------
    // Formatting helpers
    // ------------------------------------------------------------------

    private static string Gates(ScheduleEntry e)
    {
        var parts = new List<string>();
        if (e.RequiresFlag != null)
        {
            parts.Add($"requires `{e.RequiresFlag}`");
        }
        if (e.ForbidsFlag != null)
        {
            parts.Add($"forbids `{e.ForbidsFlag}`");
        }
        if (e.InSeason is { } season)
        {
            parts.Add($"{season.ToString().ToLowerInvariant()} only");
        }
        return parts.Count == 0 ? "" : $"[{string.Join("; ", parts)}] ";
    }

    private static string Window(int startMinute, int endMinute)
    {
        if (startMinute == 0 && endMinute == GameTime.MinutesPerDay)
        {
            return "all day";
        }
        string start = new GameTime(startMinute).ToClockString();
        string end = endMinute >= GameTime.MinutesPerDay
            ? "close (2:00 AM)"
            : new GameTime(endMinute).ToClockString();
        return $"{start}–{end}";
    }
}
