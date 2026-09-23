namespace TheHaunt.World;

/// <summary>
/// A map whose ground is a per-cell surface grid — what each cell IS before it is any
/// particular tile: <see cref="ExteriorMap"/> and the farm (<see cref="TestMap"/>).
/// Read-only; the world dump (<see cref="WorldDump"/>) is its reader. Names are ground
/// kinds, consistent across maps: the farm's unsealed wagon road reports "Dirt",
/// because it paints from the same dirt set as the strip's unsealed roads.
/// </summary>
public interface ISurfaceGrid
{
    int GridWidth { get; }
    int GridHeight { get; }

    /// <summary>The surface kind of cell (x, y); row 0 is north.</summary>
    string SurfaceName(int x, int y);
}
