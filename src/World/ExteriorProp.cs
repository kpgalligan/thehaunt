using Godot;

namespace TheHaunt.World;

/// <summary>
/// One entry in an <see cref="ExteriorMap"/>'s prop catalog: how a placed prop id
/// becomes its node, and which cells it blocks. The builder constructs the node the
/// map always built (name, properties, children), positioned from the placement; the
/// footprint is the blocked cells as offsets from the placement's cell (up = -y), so a
/// facade anchored on its base row blocks the rows above it. An empty footprint blocks
/// nothing (a pole sign's read area carries its own blocker).
/// </summary>
public sealed class ExteriorProp
{
    public ExteriorProp(Func<MapPlacement, Node2D> build, params Rect2I[] footprint)
    {
        Build = build;
        Footprint = footprint;
    }

    /// <summary>The prop's node, positioned from its placement.</summary>
    public Func<MapPlacement, Node2D> Build { get; }

    /// <summary>Blocked cells, as offsets from the placement's cell (up = -y).</summary>
    public IReadOnlyList<Rect2I> Footprint { get; }

    /// <summary>A footprint <paramref name="tiles"/> wide whose bottom row is the
    /// placement's row, <paramref name="rows"/> deep.</summary>
    public static Rect2I Rows(int tiles, int rows) => new(0, 1 - rows, tiles, rows);
}
