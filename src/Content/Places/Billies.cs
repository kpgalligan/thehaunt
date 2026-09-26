using TheHaunt.Core;

namespace TheHaunt.Content;

/// <summary>
/// Billie's frame: the dive bar east of the west entry, and the pit.
/// CANON (Kevin, 2026-08-26) — the bar: Jane is barely welcome when the game
/// starts, and both the patrons and the proprietor let her know it. A handful of
/// scraggly drunks at the bar at all open hours, in SHIFTS — openers, mid-afternoon
/// replacements, then the evening drunks and a few more ordinary locals — with Bud
/// at the end of the bar the entire time (the shift tables live with the characters:
/// Pete, Moody, Lyle, Harriet, Ray, Nora; canon says "some leave and are replaced",
/// so the schedule seams overlap rather than swapping on the hour).
/// CANON — THE PIT: outside Billie's, in the same frame, a deep hole, covered early
/// game. NOBODY TALKS ABOUT IT (writing rule, test-enforced:
/// Content_ActOneNeverNamesThePit — the sign may say DANGER; the people never say
/// pit). Access is chained off with a warning sign. DEFERRED: "That's a later
/// story."
/// </summary>
public static class Billies
{
    public const string MapId = MapIds.Billies;

    // [KEVIN] placeholder copy. The bar is NAMED in canon — Billie's is Billie's.
    // The pit's warning sign restates the one thing anyone will say about it (the
    // sign may say DANGER; the people never say pit — the writing rule).
    public const string BarSignText = "Billie's.";
    public const string PitSignText = "DANGER. KEEP OUT.";

    /// <summary>Copy for this place's placed signs, by placement id. Null = an unpromoted
    /// sign (a board still carrying its own map-file text).</summary>
    public static string? SignTextFor(string placementId) => placementId switch
    {
        "BarSign" => BarSignText,
        "PitSign" => PitSignText,
        _ => null,
    };
}
