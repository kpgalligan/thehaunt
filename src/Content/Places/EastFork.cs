using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// The east fork: the frame between the town centre and the east entry. Abe's shack
/// stands south of the road (Characters/Abe — twenty years, never bought a fence
/// post); the drive-in's driveway drops south through the trees (Places/DriveIn,
/// chained per D4); and north of the road a drive dead-ends at a chain.
/// PLANNED — THE MANSION (Kevin, 2026-08-26): north of the fork, in ruins — gothic
/// and creepy, foliage overgrown. No access early game; it plays a significant role
/// later. The ruin itself stays out of frame (somewhere beyond the treeline), and
/// its chain's sign admits nothing about it — not even that anyone owns it.
/// </summary>
public static class EastFork
{
    public const string MapId = MapIds.EastFork;

    // [KEVIN] placeholder copy. The mansion chain admits nothing about the mansion,
    // not even that anyone owns it; the theater board carries the drive-in doc's
    // canon words and stands only while the chain is up (its copy is only true then).
    public const string MansionChainSignText = "KEEP OUT.";
    public const string TheaterChainSignText = "PRIVATE PROPERTY. CLOSED.";
}
