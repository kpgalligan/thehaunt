using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The scripted intro's multi-speaker beat dialogues — they belong to the ARC, not to
/// any one character, so they live beside <see cref="IntroRules"/> rather than in a
/// character file. Started exclusively by StoryDirector; during their pending window
/// the staged NPCs are present-but-silent (their Talk selectors return null).
/// Intro copy is [KEVIN] placeholder: strict canon restatement, role labels only
/// (the intro cast stays unnamed — Kevin, 2026-09-01 / D3).
/// </summary>
public static class IntroBeats
{
    // NOTE: All is declared at the BOTTOM of the class — C# initialises static
    // members in textual order, and a registry field above the defs it lists would
    // capture nulls (src/Content/CLAUDE.md, the static-init rule).

    // The crew beat: surprised in a bad way to find a new owner; the player explains
    // the handwritten-contract purchase; one converging 2-way choice; ends telling the
    // player to attend the town hall meeting tonight. Every terminal path's node
    // carries SetsFlag = CrewArrivalDone (test-enforced, DFS).
    public static DialogueDef CrewArrival { get; } = Say.Graph("intro_crew_arrival", "arrival",
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
    public static DialogueDef TownMeeting { get; } = Say.Graph("intro_town_meeting", "open",
        TownMeetingNodes());

    // The overslept summons (Kevin's copy, 2026-08-28): the player was told to
    // attend and went to bed instead, so Main's sleep flow relocated the wake to
    // the hall (IntroRules.WakesAtTownHall) and this variant opens with the walk
    // the player never saw. Same meeting nodes after the narration, so the copy
    // and the terminal MeetingDone stamp can never diverge between the two doors
    // into the meeting.
    public static DialogueDef TownMeetingOverslept { get; } = Say.Graph(
        "intro_town_meeting_overslept", "nap",
        new[]
        {
            new DialogueNode("nap", new[]
            {
                new DialogueLine("", "(After a much needed nap, you made your way to City Hall. It sounded important...)"),
            }, NextNodeId: "open"),
        }.Concat(TownMeetingNodes()).ToArray());

    private static DialogueNode[] TownMeetingNodes() => new[]
    {
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
        }, SetsFlag: StoryKeys.MeetingDone),
    };

    public static IReadOnlyList<DialogueDef> All { get; } = new[]
    {
        CrewArrival, TownMeeting, TownMeetingOverslept,
    };
}
