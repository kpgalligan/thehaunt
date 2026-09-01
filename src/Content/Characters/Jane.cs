namespace TheHaunt.Content;

/// <summary>
/// Jane — the player. No NpcDef and no Talk: she is never staged or scheduled; her
/// sheet is assets/sprites/character.png (CharacterSprites.SheetPath), and her
/// wardrobe — packed-a-trailer-and-drove, nothing bought for the countryside — is
/// the art contract's (cast-sprites handoff).
///
/// CANON backstory (Kevin, 2026-08-26; the farming/mechanic thread
/// amended 2026-09-01 / D1, superseding design.md's earlier college/no-siblings
/// variant):
///
/// Jane grew up on a farm, and the family eventually lost it. Her father did a lot
/// of the work on the farm's equipment himself while they owned it, and after they
/// lost it he worked as a mechanic full time — he would bring Jane to work with
/// him and taught her everything he knew about cars (the root of the
/// mechanical-repair skill, and the reason the west-entry garage is hers to run).
///
/// She was living in a large city and had just been laid off. She was married and
/// had a child, but the child died in a tragic fall down a flight of stairs; her
/// partner blamed her for the death and suddenly left one day. They had little
/// money, so the divorce was uncomplicated. Jane packed everything she owned into
/// a small moving trailer and drove across country with no clear goal or
/// destination. She decided long ago that farming wasn't the life she wanted — but
/// she missed it, especially with so much heartache, and dreams of a simple life
/// away from the realities she's experienced.
///
/// She stumbled onto the town and found it odd that such a nice town wasn't on the
/// map on her phone. On the north outskirts she found the farm: a FOR SALE sign,
/// old and tattered, and an old man with the sun-battered look of a farmer
/// repairing it. He wanted to move back closer to family — word had come that his
/// wealthy sister was dying — but wanted to sell before he left. His price was too
/// good to be true and Jane didn't question it: a handwritten agreement on the
/// spot and a check for pretty much everything she had (after a few glasses of the
/// fantastic wine the farmer claimed was his own brew — see Places/Farm for what
/// that wine actually was). That sealed her fate: she was now a resident, bound to
/// the curse. The farmer promptly left; we'll never hear from him again — except
/// through the unsigned farewell letter waiting in the mailbox (LetterDefs).
/// </summary>
public static class Jane
{
    public const string Id = "jane";
}
