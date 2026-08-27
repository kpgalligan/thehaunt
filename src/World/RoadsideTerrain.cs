using Godot;

namespace TheHaunt.World;

/// <summary>
/// The town TileSet plus the generated roadside source, for the frames that poured
/// asphalt over this town's dirt (the motel lot, the drive-in). Like every TileSet
/// builder it (a) builds on a PRIVATE copy — <see cref="TownTerrain.LoadCopy"/> is
/// cache-ignoring — and (b) is idempotent; both are load-bearing, see
/// <see cref="TownTerrain"/> for the scar tissue.
/// </summary>
public static class RoadsideTerrain
{
    /// <summary>Atlas source id the generated sheet registers under. The shipped town
    /// atlas is source 0; painters address the two by id.</summary>
    public const int SourceId = 1;

    private static TileSet? _cached;

    /// <summary>Shared, immutable after the first call — maps only ever read it.</summary>
    public static TileSet Get() => _cached ??= Build();

    private static TileSet Build()
    {
        TileSet tileSet = TownTerrain.LoadCopy();

        TileSetTools.AddWalkableLayer(tileSet);
        TileSetTools.MakeBlocker((TileSetAtlasSource)tileSet.GetSource(0), TerrainTiles.Blocker);

        if (!tileSet.HasSource(SourceId))
        {
            var source = new TileSetAtlasSource
            {
                Texture = BuildSheet(),
                TextureRegionSize = new Vector2I(MapRoot.TileSize, MapRoot.TileSize),
            };
            for (int x = 0; x < RoadsideTiles.Columns; x++)
                source.CreateTile(new Vector2I(x, 0));
            tileSet.AddSource(source, SourceId);
        }

        TileSetTools.DeriveWalkable(tileSet);
        return tileSet;
    }

    // Palette and mix ratios straight from the handoff's ground table: asphalt 8%
    // dark, 8% light over stone-shade; concrete 7% pale, 5% mid over stone-light.
    private static readonly Color AsphaltBase = new("575a58");
    private static readonly Color AsphaltDark = new("3e4241");
    private static readonly Color AsphaltLight = new("7a7a7a");
    private static readonly Color ConcreteBase = new("9a9a8a");
    private static readonly Color ConcretePale = new("b8b5a5");
    private static readonly Color ConcreteMid = new("7a7a7a");

    private static ImageTexture BuildSheet()
    {
        const int size = MapRoot.TileSize;
        var img = Image.CreateEmpty(RoadsideTiles.Columns * size, size, false, Image.Format.Rgba8);
        for (int col = 0; col < RoadsideTiles.Columns; col++)
        {
            bool asphalt = col < 4;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int roll = Mottle(col, x, y);
                    Color c = asphalt
                        ? roll < 8 ? AsphaltDark : roll < 16 ? AsphaltLight : AsphaltBase
                        : roll < 7 ? ConcretePale : roll < 12 ? ConcreteMid : ConcreteBase;
                    if (col == 6 && y >= size - 2)
                        c = ConcretePale; // the kerb lip
                    img.SetPixel(col * size + x, y, c);
                }
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Deterministic per-pixel roll 0-99, seeded per column so the variant
    /// tiles differ — the same speckle idea as the grass detail hash.</summary>
    private static int Mottle(int col, int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093 ^ y * 19349663 ^ (col + 1) * 83492791);
            h ^= h >> 13;
            h *= 2654435761;
            h ^= h >> 16;
            return (int)(h % 100);
        }
    }
}
