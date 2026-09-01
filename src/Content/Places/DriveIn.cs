using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The dead drive-in theater, off the south side of the east fork's road.
/// CANON (Kevin, drive-in doc): it used to be a major place for the town to come
/// together — movies, an arcade, a bar, and a set of stands outside for residents
/// who didn't drive — and it shut down years ago; nothing there works. A chain
/// blocks the drive from the road, with a sign that the theater is private property
/// and closed; it keeps Jane out until SUMMER, when the chain is down from 9 AM to
/// 6 PM (built 2026-09-01 per D4 — EastForkMap wears the barrier, and the
/// <see cref="ChainDown"/> window is also exactly Shelly's schedule, so the person
/// who opens the chain is always there while it is open).
/// The refurbishment arc — buying it from Shelly, the projectors, the audio, the
/// seating, the arcade power — is PLANNED: see Story/DriveInArc.
/// </summary>
public static class DriveIn
{
    public const int OpenMinute = 180, CloseMinute = 720;   // 9:00 AM - 6:00 PM

    /// <summary>True while the entry chain is open: summer, within the open window.
    /// A pure read of the clock — the east fork's barrier, its sign and its south
    /// exit all derive from this one answer.</summary>
    public static bool ChainDown(GameTime now) =>
        now.Season == Season.Summer
        && now.MinuteOfDay >= OpenMinute && now.MinuteOfDay < CloseMinute;
}
