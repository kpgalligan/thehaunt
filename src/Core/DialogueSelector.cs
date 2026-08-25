namespace TheHaunt.Core;

// Ambient-talk selection only. Beat dialogues (intro_crew_arrival,
// intro_town_meeting) are started exclusively by StoryDirector — during their
// pending window the NPC is present-but-silent (null ⇒ no Talk prompt).
public static class DialogueSelector
{
    // Total: unknown roles and hostile flag combinations return null, never throw.
    public static string? ForNpc(string roleId, GameData data, GameTime now)
    {
        bool meetingDone = data.HasFlag(StoryKeys.MeetingDone);
        bool crewDone = data.HasFlag(StoryKeys.CrewArrivalDone);
        return roleId switch
        {
            "foreman" => meetingDone ? "foreman_after" : crewDone ? "foreman_wait" : null,
            "crew_worker_a" or "crew_worker_b" => crewDone || meetingDone ? "crew_worker_default" : null,
            "mayor" => meetingDone ? "mayor_after" : null,
            _ => null,
        };
    }
}
