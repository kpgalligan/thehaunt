namespace TheHaunt.Core;

// Ambient-talk selection only. Beat dialogues (intro_crew_arrival,
// intro_town_meeting) are started exclusively by StoryDirector — during their
// pending window the NPC is present-but-silent (null ⇒ no Talk prompt).
//
// The road-strip cast (docs/story/cast.md) varies three ways, all pure reads of
// (flags, clock): Walt tracks the time of day (canon: quiet mornings, insightful
// 2-5 PM, maudlin after), the guarded locals swap franker variants once the
// meeting has happened, and the fixtures alternate lines by day parity so two
// visits in a row are never identical.
public static class DialogueSelector
{
    // Total: unknown roles and hostile flag combinations return null, never throw.
    public static string? ForNpc(string roleId, GameData data, GameTime now)
    {
        bool meetingDone = data.HasFlag(StoryKeys.MeetingDone);
        bool crewDone = data.HasFlag(StoryKeys.CrewArrivalDone);
        bool oddDay = now.DayIndex % 2 == 1;
        return roleId switch
        {
            "foreman" => meetingDone ? "foreman_after" : crewDone ? "foreman_wait" : null,
            "crew_worker_a" or "crew_worker_b" => crewDone || meetingDone ? "crew_worker_default" : null,
            "mayor" => meetingDone ? "mayor_after" : null,

            "walt" => now.MinuteOfDay < 480 ? "walt_morning"   // before 2:00 PM
                : now.MinuteOfDay < 660 ? "walt_sharp"          // the good hours, 2-5
                : "walt_low",
            "pell" => "pell_default",
            "dennis" => oddDay ? "dennis_b" : "dennis_a",
            "gloria" => meetingDone ? "gloria_after" : "gloria_before",
            "billie" => meetingDone ? "billie_after" : "billie_before",
            "bud" => oddDay ? "bud_b" : "bud_a",
            "pete" => "pete_default",
            "moody" => "moody_default",
            "lyle" => "lyle_default",
            "harriet" => meetingDone ? "harriet_after" : "harriet_before",
            "ray" => "ray_default",
            "nora" => "nora_default",
            "sam" => oddDay ? "sam_b" : "sam_a",
            "abe" => meetingDone ? "abe_after" : "abe_before",
            // Mike tracks the shop floor, not the clock: a car WAITING ON WORK
            // swaps his line (pure read of GarageJobs — jobs are model state like
            // flags). Completed cars parked till dawn don't count: "work's
            // waiting" must never be a lie.
            "mike" => HasOpenGarageJob(data) ? "mike_jobs" : "mike_idle",
            _ => null,
        };
    }

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
}
