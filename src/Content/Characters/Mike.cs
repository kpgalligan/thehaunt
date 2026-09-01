using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Mike — the garage clerk. Name is Kevin's (canon, 2026-08-30).
/// CANON (Kevin, 2026-08-30): hired with the garage deed; friendly but NOT a
/// mechanic — his only job is taking new customers and collecting money, and his
/// word is how Jane learns a car came in ("Word from Mike", QuestToastUi). At the
/// counter every open hour, 9am-6pm, seven days; the hours gate customers and Mike,
/// never Jane.
/// PROPOSED [KEVIN] (2026-08-30): he likes the job precisely because he can't do the
/// other half of it; writes down what customers say about their cars verbatim,
/// spelling and all, and hands Jane the page like it's evidence. Voice: cheerful,
/// unembarrassed, first-name-basis with everyone by day two.
/// Look: cast_west block 4 (soft cap, warm plain shirt — the counter's clothes, not
/// the pit's; appended append-only per the handoff README's 2026-08-30 amendment).
/// </summary>
public static class Mike
{
    public const string Id = "mike";

    // Hired with the deed — the flag gate is the whole "once owned" rule — and at
    // his counter every day the shop is open. The window derives from GarageOpsRules'
    // hours so Mike's presence and the customer-arrival window can never diverge
    // (the Shopkeeper<->ShopHours and Dennis<->GasStation binding). Open end of the
    // counter, keeper rule.
    public static readonly ScheduleEntry[] Schedule =
    {
        new(StoryKeys.GarageDeed, null,
            GarageOpsRules.OpenMinuteOfDay, GarageOpsRules.CloseMinuteOfDay,   // 9:00 AM - 6:00 PM
            new NpcPlacement(MapIds.GarageInterior, 10, 4, 0, Ambit: 1)),
    };

    // Canon: friendly, NOT a mechanic; his only job is taking new customers and
    // collecting money. Both defs restate exactly that — no wrench in his hand,
    // ever. [KEVIN] invented copy
    public static readonly DialogueDef Idle = Say.Linear("mike_idle", Id,
        "Quiet so far, boss. Anybody rolls in, I'll take their keys and get you word.",
        "Don't ask me what's wrong with the cars, though. I write down what the customer says and point at you.");

    // [KEVIN] invented copy
    public static readonly DialogueDef Jobs = Say.Linear("mike_jobs", Id,
        "Work's waiting on the lift — I logged what they asked for. Keys are in it.",
        "You do the fixing, I do the smiling. I'll collect once they're happy.");

    // Mike tracks the shop floor, not the clock: a car WAITING ON WORK swaps his
    // line (pure read of GarageJobs — jobs are model state like flags). Completed
    // cars parked till dawn don't count: "work's waiting" must never be a lie.
    public static string? Talk(GameData data, GameTime now) =>
        HasOpenGarageJob(data) ? Jobs.Id : Idle.Id;

    private static bool HasOpenGarageJob(GameData data)
    {
        foreach (GarageJobRecord job in data.GarageJobs)
        {
            if (!job.Completed)
            {
                return true;
            }
        }
        return false;
    }

    public static readonly CharacterDef Def = new(
        new NpcDef(Id, "Mike", CastSheets.West, 4, Schedule), Talk, new[] { Idle, Jobs });
}
