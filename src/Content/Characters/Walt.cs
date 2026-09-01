using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Walt — the motel proprietor. West entry; the lobby is his life. Name canon
/// (proposed 2026-08-27; Kevin accepted the cast names 2026-09-01 / D2).
/// CANON (Kevin, 2026-08-26): middle-aged alcoholic; gruff, but steeped in town
/// rumor — an unreliable information source, some of it useful. Doesn't talk much in
/// the morning, most insightful from about 2 PM to 5 PM, and from there on mostly
/// sad about his lost wife and failing business. The motel is where many of the
/// sacrifices will ultimately come from, when they are strangers — Walt doesn't
/// know that part is his to host.
/// PROPOSED [KEVIN] (2026-08-27 commission): his wife June, "lost" eleven years ago
/// — "lost" is all he ever says; he lights two lamps of an evening (the lobby and
/// the one paying room); the registry is the one thing he keeps precise. Voice:
/// sandpaper declaratives; the fourth drink makes him accurate before it makes him
/// sad. DEFERRED: his sentence about Pell stops mid-word and stays stopped (Act I).
/// Look: cast_west block 0 (the art contract owns wardrobe; never restate it here).
/// </summary>
public static class Walt
{
    public const string Id = "walt";

    // Behind the motel desk from open to close of the day: the lobby IS Walt's life.
    // His conversation, not his placement, tracks the clock (Talk below).
    public static readonly ScheduleEntry[] Schedule =
    {
        new(null, null,
            0, GameTime.MinutesPerDay,
            new NpcPlacement(MapIds.Motel, 4, 4, 1, Ambit: 1)), // open end of the desk
    };

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef Morning = Say.Linear("walt_morning", Id,
        "Mm.",
        "Coffee's not on. If it's talk you're after, come back after two. I'm better after two.");

    // The rumor tap — the one hub outside the intro beats. Every spoke returns to
    // the hub; the exit is the only terminal. [KEVIN] invented copy
    public static readonly DialogueDef Sharp = Say.Graph("walt_sharp", "open",
        new DialogueNode("open", new[]
        {
            new DialogueLine(Id, "Ask, then. These are my good hours — two to five, give or take. The rest of the day I'm no use to anybody, and I'd know."),
        }, NextNodeId: "hub"),
        new DialogueNode("hub", new[]
        {
            new DialogueLine(Id, "What'll it be?"),
        }, Choices: new[]
        {
            new DialogueChoice("Tell me about the motel.", "motel"),
            new DialogueChoice("What should I know about this town?", "town"),
            new DialogueChoice("Anyone else staying here?", "guests"),
            new DialogueChoice("That's all for now.", "closing"),
        }),
        new DialogueNode("motel", new[]
        {
            new DialogueLine(Id, "Four rooms. June ran them full once — fresh flowers, the whole business. Now I light two lamps of an evening and that's plenty."),
            new DialogueLine(Id, "Guests come through. Salesmen, hunters, folks who took a wrong turn. They stay a night and move on. Mostly."),
        }, NextNodeId: "hub"),
        new DialogueNode("town", new[]
        {
            new DialogueLine(Id, "If nobody's told you how this town is yet, the mayor will — go to a meeting. If they have, then you know what everyone knows."),
            new DialogueLine(Id, "Here's the part they don't say: the town doesn't hate anybody. It's not personal. Weather isn't personal either."),
            new DialogueLine(Id, "Keep a lamp lit at night. Everybody here does. Old habit. Nobody remembers starting it."),
        }, NextNodeId: "hub"),
        new DialogueNode("guests", new[]
        {
            new DialogueLine(Id, "One, right now. Fella in room three — Pell. Notions salesman. Been here three weeks on a one-night rate."),
            new DialogueLine(Id, "Keeps telling me he'll head out tomorrow. Says it smiling. That's the part I'd—"),
            new DialogueLine(Id, "Forget it. Ask me something else."),
        }, NextNodeId: "hub"),
        new DialogueNode("closing", new[]
        {
            new DialogueLine(Id, "Then take your good hours while you've got 'em. I mean that generally."),
        }));

    // [KEVIN] invented copy (2026-08-27 commission)
    public static readonly DialogueDef Low = Say.Linear("walt_low", Id,
        "June kept the books, you know. I keep them now. The numbers get smaller every year — you'd think they'd take less ink.",
        "Four rooms. Two lamps. You don't want a room. Nobody wants a room.");

    // Canon clock: quiet mornings, the good hours 2-5, then June and the ledger.
    public static string? Talk(GameData data, GameTime now) =>
        now.MinuteOfDay < 480 ? Morning.Id   // before 2:00 PM
        : now.MinuteOfDay < 660 ? Sharp.Id   // the good hours, 2-5
        : Low.Id;

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Walt", CastSheets.West, 0, Schedule), Talk,
        new[] { Morning, Sharp, Low });
}
