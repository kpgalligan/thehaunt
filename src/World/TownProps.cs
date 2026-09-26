using Godot;

namespace TheHaunt.World;

/// <summary>
/// Source rects for the town prop sheet (assets/sprites/town/props.png), verbatim
/// from the art handoff §5. Every rect is drawn base-down, so a prop's
/// <see cref="Prop.Source"/> pairs with a bottom-centre anchor.
/// </summary>
public static class TownProps
{
    public const string TexturePath = "res://assets/sprites/town/props.png";

    public static readonly Rect2 Well = new(0, 0, 32, 32);
    public static readonly Rect2 BenchA = new(32, 16, 32, 16);
    public static readonly Rect2 BenchB = new(64, 16, 32, 16);
    public static readonly Rect2 NoticeBoard = new(96, 0, 32, 32);
    public static readonly Rect2[] Planters =
    {
        new(128, 16, 16, 16), new(144, 16, 16, 16), new(160, 16, 16, 16),
    };

    // Window states are a swappable layer, addressable per building: almost every
    // dread beat in the act escalation is a window state change (art handoff §5).
    public static readonly Rect2 WindowLit = new(208, 0, 16, 16);
    public static readonly Rect2 WindowDark = new(224, 0, 16, 16);
    public static readonly Rect2 WindowShuttered = new(240, 0, 16, 16);

    // The town's placement ids — what a "prop" record names. The first three are not
    // sheet props (TownMap builds them itself); the rest resolve through ByName.
    public const string TownHallId = "town_hall", GeneralStoreId = "general_store", StreetLightId = "street_light",
        WellId = "well", BenchAId = "bench_a", BenchBId = "bench_b", NoticeBoardId = "notice_board";
    public static readonly string[] PlanterIds = { "planter_0", "planter_1", "planter_2" };

    /// <summary>A sheet prop's source rect by placement id. Throws, listing the known ids, for any other name.</summary>
    public static Rect2 ByName(string id)
    {
        switch (id)
        {
            case WellId: return Well;
            case BenchAId: return BenchA;
            case BenchBId: return BenchB;
            case NoticeBoardId: return NoticeBoard;
        }
        int planter = Array.IndexOf(PlanterIds, id);
        if (planter >= 0)
            return Planters[planter];
        throw new ArgumentException(
            $"Unknown town prop '{id}'. Known: {string.Join(", ", SheetIds)}.", nameof(id));
    }

    /// <summary>Every id <see cref="ByName"/> resolves.</summary>
    internal static IEnumerable<string> SheetIds =>
        new[] { WellId, BenchAId, BenchBId, NoticeBoardId }.Concat(PlanterIds);
}
