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

    // Palette and mix ratios straight from the handoff's ground table: lot 8% dark,
    // 10% light over stone-shade; road 16% shade, 6% ink over stone-dark (a full
    // value-step darker than the lot); concrete 7% pale, 5% mid over stone-light.
    private static readonly Color LotBase = new("575a58");
    private static readonly Color LotDark = new("3e4241");
    private static readonly Color LotLight = new("7a7a7a");
    private static readonly Color RoadBase = new("3e4241");
    private static readonly Color RoadShade = new("575a58");
    private static readonly Color RoadInk = new("2b241d");
    private static readonly Color ConcreteBase = new("9a9a8a");
    private static readonly Color ConcretePale = new("b8b5a5");
    private static readonly Color ConcreteMid = new("7a7a7a");

    /// <summary>A parking-lot pixel for a roll — shared with the road dressing so
    /// kerb cuts are the same asphalt as the lot they serve.</summary>
    internal static Color LotPixel(int roll) =>
        roll < 8 ? LotDark : roll < 18 ? LotLight : LotBase;

    internal static Color RoadPixel(int roll) =>
        roll < 16 ? RoadShade : roll < 22 ? RoadInk : RoadBase;

    internal static Color ConcretePixel(int roll) =>
        roll < 7 ? ConcretePale : roll < 12 ? ConcreteMid : ConcreteBase;

    private static ImageTexture BuildSheet()
    {
        const int size = MapRoot.TileSize;
        var img = Image.CreateEmpty(RoadsideTiles.Columns * size, size, false, Image.Format.Rgba8);
        for (int col = 0; col < RoadsideTiles.Columns; col++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int roll = Mottle(col, x, y);
                    Color c = col < 4 ? LotPixel(roll)
                        : col < 7 ? ConcretePixel(roll)
                        : RoadPixel(roll);
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
    internal static int Mottle(int col, int x, int y)
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
