namespace TheHaunt.Content;

/// <summary>
/// The packed cast atlases (cast-sprites handoff, 2026-08-27): one 96x96 block per
/// character, block order fixed by the handoff README and APPEND-ONLY (Mike's 2026-08-30
/// block-4 amendment is the precedent). Per-character wardrobe lives in the art;
/// changing clothes is a gen_cast.js spec edit + re-run (tools/run_gen_cast.mjs),
/// never a code change and never a repaint.
/// </summary>
public static class CastSheets
{
    public const string Town = "res://assets/sprites/cast/cast_town.png";
    public const string West = "res://assets/sprites/cast/cast_west.png";
    public const string Billies = "res://assets/sprites/cast/cast_billies.png";
    public const string East = "res://assets/sprites/cast/cast_east.png";
}
