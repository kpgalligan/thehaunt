namespace TheHaunt.Core;

// Intro copy is [KEVIN] placeholder: strict canon restatement (docs/design.md), role
// labels only. The road-strip cast's copy (walt_* onward) is invented under Kevin's
// 2026-08-27 commission, per the voices in docs/story/cast.md — pending review, so
// each def carries one [KEVIN] marker rather than one per line. Tone law for all of
// it: Act I dread lives only in dialogue, only at look-twice-and-move-on strength.
public static class DialogueDefs
{
    // Insertion order below is the canonical iteration order for All.
    public static IReadOnlyDictionary<string, DialogueDef> All { get; } = Build();

    // Missing id here is a code bug — throws KeyNotFoundException.
    public static DialogueDef Get(string id) => All[id];

    // Null-tolerant lookup for ids coming from callers that tolerate absence.
    public static DialogueDef? TryGet(string id) => All.TryGetValue(id, out var def) ? def : null;

    private static Dictionary<string, DialogueDef> Build()
    {
        var defs = new[]
        {
            BuildCrewArrival(),
            BuildTownMeeting(),
            BuildForemanWait(),
            BuildForemanAfter(),
            BuildMayorAfter(),
            BuildCrewWorkerDefault(),
            BuildWaltMorning(),
            BuildWaltSharp(),
            BuildWaltLow(),
            BuildPell(),
            BuildDennisA(),
            BuildDennisB(),
            BuildGloriaBefore(),
            BuildGloriaAfter(),
            BuildBillieBefore(),
            BuildBillieAfter(),
            BuildBudA(),
            BuildBudB(),
            BuildPete(),
            BuildMoody(),
            BuildLyle(),
            BuildHarrietBefore(),
            BuildHarrietAfter(),
            BuildRay(),
            BuildNora(),
            BuildSamA(),
            BuildSamB(),
            BuildAbeBefore(),
            BuildAbeAfter(),
        };
        return defs.ToDictionary(d => d.Id);
    }

    private static DialogueDef Def(string id, string startNodeId, params DialogueNode[] nodes) =>
        new(id, startNodeId, nodes.ToDictionary(n => n.Id));

    // The crew beat: surprised in a bad way to find a new owner; the player explains
    // the handwritten-contract purchase; one converging 2-way choice; ends telling the
    // player to attend the town hall meeting tonight. Every terminal path's node
    // carries SetsFlag = CrewArrivalDone (test-enforced, DFS).
    private static DialogueDef BuildCrewArrival() => Def("intro_crew_arrival", "arrival",
        new DialogueNode("arrival", new[]
        {
            new DialogueLine("foreman", "Hey — hold on. There's somebody up at the house."),   // [KEVIN]
            new DialogueLine("foreman", "We're the repair crew, out of town. Came to clear the road and see to the old farm. Nobody said a word about a new owner."),   // [KEVIN]
            new DialogueLine("crew_worker_a", "Boss... nobody's bought this place in years."),   // [KEVIN]
        }, NextNodeId: "explain"),
        new DialogueNode("explain", new[]
        {
            new DialogueLine("", "You tell them how it happened — the road trip, the farm sitting empty, a price too good to be true."),   // [KEVIN]
            new DialogueLine("", "A handwritten contract, a check, and the place was yours."),   // [KEVIN]
        }, NextNodeId: "react"),
        new DialogueNode("react", new[]
        {
            new DialogueLine("foreman", "A handwritten contract."),   // [KEVIN]
            new DialogueLine("foreman", "...Well. Signed is signed. It's done."),   // [KEVIN]
        }, Choices: new[]
        {
            new DialogueChoice("It's a fresh start. I could use one.", "fork_hopeful"),   // [KEVIN]
            new DialogueChoice("Why does everyone keep saying it like that?", "fork_wary"),   // [KEVIN]
        }),
        new DialogueNode("fork_hopeful", new[]
        {
            new DialogueLine("foreman", "A fresh start. Sure. It's good land, and it's a pleasant town — folks here look after each other."),   // [KEVIN]
            new DialogueLine("foreman", "You'll see that soon enough."),   // [KEVIN]
        }, NextNodeId: "invite"),
        new DialogueNode("fork_wary", new[]
        {
            new DialogueLine("foreman", "No reason. Only that the old places don't sell often, and... it's not my place to explain."),   // [KEVIN]
            new DialogueLine("foreman", "You'll get it all straight tonight. I promise you that."),   // [KEVIN]
        }, NextNodeId: "invite"),
        new DialogueNode("invite", new[]
        {
            new DialogueLine("foreman", "There's a town meeting at the hall tonight. You need to be there."),   // [KEVIN]
            new DialogueLine("foreman", "The mayor explains things to newcomers. Better you hear it from the mayor than from us."),   // [KEVIN]
            new DialogueLine("foreman", "We'll be around — the storm left plenty to fix. Go on about your day, but come tonight. Don't forget."),   // [KEVIN]
        }, SetsFlag: StoryKeys.CrewArrivalDone));

    // The meeting beat: mayor hub-and-spoke Q&A — each spoke strictly restates canon
    // and returns to the hub; the exit choice is the only terminal path and sets
    // MeetingDone (re-raising on hub re-entry would be harmless — only-if-absent).
    private static DialogueDef BuildTownMeeting() => Def("intro_town_meeting", "open",
        new DialogueNode("open", new[]
        {
            new DialogueLine("mayor", "Ah — our new neighbor. Come in, take a seat."),   // [KEVIN]
            new DialogueLine("mayor", "I'll be plain, because you deserve plain: you bought property in this town. That means you cannot leave it. None of us can."),   // [KEVIN]
            new DialogueLine("mayor", "There is more you should know — about selling, about the tribute. Ask, and I'll answer honestly."),   // [KEVIN]
        }, NextNodeId: "hub"),
        new DialogueNode("hub", new[]
        {
            new DialogueLine("mayor", "What would you like to know?"),   // [KEVIN]
        }, Choices: new[]
        {
            new DialogueChoice("Why can't I leave?", "why_leave"),   // [KEVIN]
            new DialogueChoice("Can I sell the farm and go?", "selling"),   // [KEVIN]
            new DialogueChoice("What is the tribute?", "tribute"),   // [KEVIN]
            new DialogueChoice("I've heard enough for tonight.", "closing"),   // [KEVIN]
        }),
        new DialogueNode("why_leave", new[]
        {
            new DialogueLine("mayor", "Try it, if you must — everyone does, once. Drive out past the west road and you'll come rolling in from the east. Leave east, and you'll come in from the west."),   // [KEVIN]
            new DialogueLine("mayor", "Something in this town wills it so. It has never once let an owner go."),   // [KEVIN]
        }, NextNodeId: "hub"),
        new DialogueNode("selling", new[]
        {
            new DialogueLine("mayor", "You can sell — if you can find a buyer. Few strangers ever find us, and the traders who do have heard the stories. They rarely buy."),   // [KEVIN]
            new DialogueLine("mayor", "And of those who sold and left... not one has ever come back. Whether they can't return, or something worse happens — nobody knows for sure."),   // [KEVIN]
        }, NextNodeId: "hub"),
        new DialogueNode("tribute", new[]
        {
            new DialogueLine("mayor", "There is an evil in this town. Most of the time it sleeps, and life here is genuinely pleasant — we work hard to keep it so."),   // [KEVIN]
            new DialogueLine("mayor", "But now and then it wakes, and it demands tribute. When it does, the town pays. That's the price of all our quiet days."),   // [KEVIN]
        }, NextNodeId: "hub"),
        new DialogueNode("closing", new[]
        {
            new DialogueLine("mayor", "Then that's enough for one night. It isn't the life you planned. It wasn't for any of us."),   // [KEVIN]
            new DialogueLine("mayor", "But it's a good town, and we look after our own. Go home, get some sleep. Your farm will want you in the morning."),   // [KEVIN]
        }, SetsFlag: StoryKeys.MeetingDone));

    private static DialogueDef BuildForemanWait() => Def("foreman_wait", "wait",
        new DialogueNode("wait", new[]
        {
            new DialogueLine("foreman", "Meeting's tonight, at the town hall. Everything gets explained there — hold your questions till then."),   // [KEVIN]
            new DialogueLine("foreman", "We've got our hands full with storm damage until dark anyway."),   // [KEVIN]
        }));

    private static DialogueDef BuildForemanAfter() => Def("foreman_after", "after",
        new DialogueNode("after", new[]
        {
            new DialogueLine("foreman", "So now you know how it is. Takes a while to sit right."),   // [KEVIN]
            new DialogueLine("foreman", "For what it's worth, it's a pleasant town, most days. You'll find your feet."),   // [KEVIN]
        }));

    private static DialogueDef BuildMayorAfter() => Def("mayor_after", "after",
        new DialogueNode("after", new[]
        {
            new DialogueLine("mayor", "Settling in? The first days are the hardest — it gets easier, I promise."),   // [KEVIN]
            new DialogueLine("mayor", "Tend your land, meet your neighbors. We look after our own here."),   // [KEVIN]
        }));

    private static DialogueDef BuildCrewWorkerDefault() => Def("crew_worker_default", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("crew_worker_a", "Half a hillside came down on that road. Took us all morning to cut a way through."),   // [KEVIN]
            new DialogueLine("crew_worker_a", "Storm left damage all over town, too. We won't be short of work for a while."),   // [KEVIN]
        }));

    // ------------------------------------------------------------------
    // The road strip (docs/story/cast.md). All ambient: no flags raised, no beats.
    // ------------------------------------------------------------------

    // Walt's canon clock: quiet mornings, insightful 2-5 PM, maudlin after. The
    // selector swaps these three by the minute of day.

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildWaltMorning() => Def("walt_morning", "morning",
        new DialogueNode("morning", new[]
        {
            new DialogueLine("walt", "Mm."),
            new DialogueLine("walt", "Coffee's not on. If it's talk you're after, come back after two. I'm better after two."),
        }));

    // The rumor tap — the one hub outside the intro beats. Every spoke returns to
    // the hub; the exit is the only terminal. [KEVIN] invented copy
    private static DialogueDef BuildWaltSharp() => Def("walt_sharp", "open",
        new DialogueNode("open", new[]
        {
            new DialogueLine("walt", "Ask, then. These are my good hours — two to five, give or take. The rest of the day I'm no use to anybody, and I'd know."),
        }, NextNodeId: "hub"),
        new DialogueNode("hub", new[]
        {
            new DialogueLine("walt", "What'll it be?"),
        }, Choices: new[]
        {
            new DialogueChoice("Tell me about the motel.", "motel"),
            new DialogueChoice("What should I know about this town?", "town"),
            new DialogueChoice("Anyone else staying here?", "guests"),
            new DialogueChoice("That's all for now.", "closing"),
        }),
        new DialogueNode("motel", new[]
        {
            new DialogueLine("walt", "Nine rooms. June ran them full once — fresh flowers, the whole business. Now I light two lamps of an evening and that's plenty."),
            new DialogueLine("walt", "Guests come through. Salesmen, hunters, folks who took a wrong turn. They stay a night and move on. Mostly."),
        }, NextNodeId: "hub"),
        new DialogueNode("town", new[]
        {
            new DialogueLine("walt", "If nobody's told you how this town is yet, the mayor will — go to a meeting. If they have, then you know what everyone knows."),
            new DialogueLine("walt", "Here's the part they don't say: the town doesn't hate anybody. It's not personal. Weather isn't personal either."),
            new DialogueLine("walt", "Keep a lamp lit at night. Everybody here does. Old habit. Nobody remembers starting it."),
        }, NextNodeId: "hub"),
        new DialogueNode("guests", new[]
        {
            new DialogueLine("walt", "One, right now. Fella in room three — Pell. Notions salesman. Been here three weeks on a one-night rate."),
            new DialogueLine("walt", "Keeps telling me he'll head out tomorrow. Says it smiling. That's the part I'd—"),
            new DialogueLine("walt", "Forget it. Ask me something else."),
        }, NextNodeId: "hub"),
        new DialogueNode("closing", new[]
        {
            new DialogueLine("walt", "Then take your good hours while you've got 'em. I mean that generally."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildWaltLow() => Def("walt_low", "low",
        new DialogueNode("low", new[]
        {
            new DialogueLine("walt", "June kept the books, you know. I keep them now. The numbers get smaller every year — you'd think they'd take less ink."),
            new DialogueLine("walt", "Nine rooms. Two lamps. You don't want a room. Nobody wants a room."),
        }));

    // The west end's one Act-I dread tell: a stranger who is far too content.
    // [KEVIN] invented copy
    private static DialogueDef BuildPell() => Def("pell_default", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("pell", "Oh — hello! Lovely evening. Or morning. It does slip past me here. Isn't that funny."),
            new DialogueLine("pell", "I sleep at this motel like I slept when I was a boy. Deeper, even. I keep meaning to push on — samples don't sell themselves — but every morning it seems to matter a little less."),
            new DialogueLine("pell", "Isn't that a kind of mercy?"),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildDennisA() => Def("dennis_a", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("dennis", "Welcome to the gas station. Pumps are hand-crank. The owner says electric's 'more trouble than it's worth out here.' The man runs a gas station."),
            new DialogueLine("dennis", "Jerky's on the shelf. It's from... a while ago. It's aged. Like wine, if wine were jerky."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildDennisB() => Def("dennis_b", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("dennis", "No, there's no signal. No, there's no tower. Yes, I've asked. Nobody else seems bothered, which is — fine. Totally normal town."),
            new DialogueLine("dennis", "We've got pills for headaches. We've got pills for... other stuff. My advice? Don't read the labels too hard."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildGloriaBefore() => Def("gloria_before", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("gloria", "Well, look at you — the one who bought the old farm. Word gets around, honey. Don't look so alarmed, it's a small town."),
            new DialogueLine("gloria", "There's things about this place that aren't mine to tell you. Go hear the mayor out first. Then come back and we'll talk like grown-ups."),
        }));

    // '74, "picking something up for a friend": the drug trade winked at, never
    // named — that reveal is deferred. [KEVIN] invented copy
    private static DialogueDef BuildGloriaAfter() => Def("gloria_after", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("gloria", "So they told you. Chin up, honey — I've been furious about it for fifty years and it hasn't spoiled my appetite yet."),
            new DialogueLine("gloria", "I came out here in '74 to pick something up for a friend, and I stayed for a man with a truck full of bad ideas. Otis. Lord, he was fun."),
            new DialogueLine("gloria", "Buy a sparkler before you go. Little lights help. That's not mysticism, that's just true."),
        }));

    // Barely welcome, canon — with one inch of decency in it. [KEVIN] invented copy
    private static DialogueDef BuildBillieBefore() => Def("billie_before", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("billie", "We're pouring for regulars tonight."),
            new DialogueLine("billie", "...Stool by the door's nobody's. If you're going to sit, sit quiet."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildBillieAfter() => Def("billie_after", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("billie", "You went to the meeting, so you know why nobody in here is looking for a new friend."),
            new DialogueLine("billie", "A drink's a drink, though. Your money's the same color as anybody's."),
        }));

    // The war and the deeper story stay unnamed (deferred). [KEVIN] invented copy
    private static DialogueDef BuildBudA() => Def("bud_a", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("bud", "Been holding this stool since before the paint on that door."),
            new DialogueLine("bud", "You'll settle, or you won't. Either way the town gets its way. Drink up."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildBudB() => Def("bud_b", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("bud", "Some places you leave. Some places you're from. I quit sorting out which is which a long way back."),
            new DialogueLine("bud", "It's a good bar. That part's simple."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildPete() => Def("pete_default", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("pete", "Morning crowd's the honest crowd. Evening folks drink to forget. We drink to get square with the day."),
            new DialogueLine("pete", "Seven letters, 'homeward.' Doesn't fit. Never fits."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildMoody() => Def("moody_default", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("moody", "Ha! A new face! Sit down, sit down — I'm everybody's friend till noon, and nobody's after three."),
            new DialogueLine("moody", "Don't mind the quiet ones in here. Quiet's just how some folks stay comfortable."),
        }));

    // Conspiracy-minded about mundane things ONLY — never the real secret.
    // [KEVIN] invented copy
    private static DialogueDef BuildLyle() => Def("lyle_default", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("lyle", "You notice the mail comes at different times? Tuesday it was nine. Thursday, noon sharp. I keep a ledger."),
            new DialogueLine("lyle", "And the well water's sweeter on the east side of town. Nobody wants to hear it. I've done tastings."),
        }));

    // The one patron who says it out loud (canon: they let Jane know).
    // [KEVIN] invented copy
    private static DialogueDef BuildHarrietBefore() => Def("harriet_before", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("harriet", "Oh, child. You bought?"),
            new DialogueLine("harriet", "...Don't mind me. That's the gin talking. Go on and have your evening."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildHarrietAfter() => Def("harriet_after", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("harriet", "I taught school in this town for thirty years. Every child in it learned two things off me: long division, and when to stop asking questions."),
            new DialogueLine("harriet", "You'll learn the second one too. The bright ones always do."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildRay() => Def("ray_default", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("ray", "Long day. We've been patching roofs since the storm — half the town lost shingles."),
            new DialogueLine("ray", "First one's for my back. Second one's for the quiet."),
        }));

    // The cozy anchor: normal life, insisting on itself. [KEVIN] invented copy
    private static DialogueDef BuildNora() => Def("nora_default", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("nora", "Don't let this room fool you — it's a good town. Potlucks, the harvest dance, the whole calendar."),
            new DialogueLine("nora", "You should come to things! Being new wears off a lot faster if you let people get a look at you."),
        }));

    // Canon: vaguely poetic, MOSTLY rhymes — the lines that don't are the ones
    // that land. No pronouns for Sam, ever. [KEVIN] invented copy
    private static DialogueDef BuildSamA() => Def("sam_a", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("sam", "A new head through my door — sit, let me see. Storm-blown and city-cut... we'll set that free."),
            new DialogueLine("sam", "Scissors know what the season knows: everything grows back. Almost everything grows."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildSamB() => Def("sam_b", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("sam", "Snip, snip — the year turns quick, the light gets thin. Come by before the frost; I'll tuck the summer in."),
            new DialogueLine("sam", "You wear your worry at the temples. I can cut around it."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    private static DialogueDef BuildAbeBefore() => Def("abe_before", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("abe", "Car quit on me right about where you're standing. Twenty years back, near enough. Built the shack that autumn."),
            new DialogueLine("abe", "Town's been kind, mostly. I run errands — fetch things from outside. Folks here find it hard to travel."),
        }));

    // The distinction that rules his life (canon: never bought, so never bound).
    // [KEVIN] invented copy
    private static DialogueDef BuildAbeAfter() => Def("abe_after", "default",
        new DialogueNode("default", new[]
        {
            new DialogueLine("abe", "So you own now. Hm. I never bought so much as a fence post here. Rented my whole life — best decision I never made on purpose."),
            new DialogueLine("abe", "You need anything from beyond the town line, you ask me. I make the trip when folks need it. Always have."),
        }));
}
