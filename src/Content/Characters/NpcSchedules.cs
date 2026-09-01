using TheHaunt.Core;

namespace TheHaunt.Content;

// Pure, deterministic schedule resolution; NPCs teleport between slots (no pathing —
// a slot change is a cut). A placement's Ambit is the view-side amble radius around
// the slot: proprietors putter about their rooms, seated patrons and beat-staged
// NPCs hold still (0). The TABLES live with their characters — one file per
// character in this folder — and entry order in each table is load-bearing
// (first match wins). All staging [KEVIN].
public static class NpcSchedules
{
    // First entry whose flags pass and whose window contains now.MinuteOfDay;
    // null = absent. Window is start-inclusive, end-exclusive.
    public static NpcPlacement? Resolve(NpcDef def, GameData data, GameTime now)
    {
        int minute = now.MinuteOfDay;
        foreach (var entry in def.Schedule)
        {
            if (entry.RequiresFlag != null && !data.HasFlag(entry.RequiresFlag))
            {
                continue;
            }
            if (entry.ForbidsFlag != null && data.HasFlag(entry.ForbidsFlag))
            {
                continue;
            }
            if (entry.InSeason is { } season && now.Season != season)
            {
                continue;
            }
            if (minute < entry.StartMinuteOfDay || minute >= entry.EndMinuteOfDay)
            {
                continue;
            }
            return entry.Placement;
        }
        return null;
    }
}
