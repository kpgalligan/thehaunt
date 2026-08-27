namespace TheHaunt.World;

/// <summary>
/// The optional field keys a placement's extras use, for the kinds that need more than
/// the one id the record guarantees — a door has a target MAP and a target SPAWN, and
/// only one of the two can be the id.
///
/// Named constants rather than literals for a specific bug: a mistyped key is not an
/// error, it is an unknown field. It would round-trip perfectly and be silently ignored
/// by the builder, which is the worst possible failure — the file says the door leads
/// somewhere and the door does not.
/// </summary>
public static class PlacementFields
{
    /// <summary>Target spawn name for a door or an exit ("entry", "road"). Default "default".</summary>
    public const string Spawn = "spawn";

    /// <summary>A sign's copy, verbatim. [KEVIN] Provisional: copy moves to a table of its own once there is one.</summary>
    public const string Text = "text";

    /// <summary>Trigger/strip width in TILES, for the kinds that cover a run of cells rather than one.</summary>
    public const string Width = "w";

    /// <summary>Trigger/strip height in TILES.</summary>
    public const string Height = "h";

    /// <summary>False for a piece the player walks over or an NPC stands on (the store's till). Default true.</summary>
    public const string Blocks = "blocks";
}
