using Godot;

namespace TheHaunt.World;

/// <summary>
/// The town TileSet plus the generated roadside source, for the frames that poured
/// asphalt over this town's dirt (the motel lot, the drive-in) — the one TileSet every
/// <see cref="ExteriorMap"/> builds with. Four sources: 0 the shipped town atlas, 1 the
/// generated roadside sheet (<see cref="RoadsideTiles"/>), 2 the generated landscape
/// sheet (<see cref="LandscapeTiles"/> — placeholder water, deep water and bush, every
/// tile a full-tile blocker), and 3 a private copy of the farm atlas, for fences (their
/// collision is the farm .tres's own). Like every TileSet builder it (a) builds on
/// PRIVATE copies — <see cref="TownTerrain.LoadCopy"/> and the farm load are
/// cache-ignoring — and (b) is idempotent; both are load-bearing, see
/// <see cref="TownTerrain"/> for the scar tissue.
/// </summary>
public static class RoadsideTerrain
{
    /// <summary>Atlas source id the generated sheet registers under. The shipped town
    /// atlas is source 0; painters address the two by id.</summary>
    public const int SourceId = 1;

    /// <summary>Atlas source id of the generated <see cref="LandscapeTiles"/> sheet;
    /// every tile a full-tile blocker.</summary>
    public const int LandscapeSourceId = 2;

    /// <summary>Atlas source id of the private copy of thehaunt_farm.tres source 0,
    /// addressed with <see cref="FarmTiles"/> coordinates — the town's fences.</summary>
    public const int FenceSourceId = 3;

    private static TileSet? _cached;

    // The farm set the fence source was lifted out of. Kept alive for the life of the
    // process, as FarmTerrain keeps its donor: letting it go would be betting on when a
    // sub-resource's last owner is allowed to disappear.
    private static TileSet? _farmDonor;

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

        // The landscape source joins the set BEFORE its blockers are stamped: a TileData
        // has no physics layers until its source belongs to a TileSet, so a collision
        // write made earlier fails silently and the water would derive walkable
        // (InteriorTerrain's order). MakeBlocker creates each tile.
        TileSetAtlasSource landscape;
        if (tileSet.HasSource(LandscapeSourceId))
        {
            landscape = (TileSetAtlasSource)tileSet.GetSource(LandscapeSourceId);
        }
        else
        {
            landscape = new TileSetAtlasSource
            {
                Texture = BuildLandscapeSheet(),
                TextureRegionSize = new Vector2I(MapRoot.TileSize, MapRoot.TileSize),
            };
            if (tileSet.AddSource(landscape, LandscapeSourceId) != LandscapeSourceId)
                throw new InvalidOperationException($"Roadside TileSet refused source {LandscapeSourceId}.");
        }
        for (int x = 0; x < LandscapeTiles.Columns; x++)
            TileSetTools.MakeBlocker(landscape, new Vector2I(x, 0));

        if (!tileSet.HasSource(FenceSourceId))
        {
            _farmDonor = ResourceLoader.Load<TileSet>(FarmTerrain.TileSetPath,
                    cacheMode: ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Farm terrain TileSet missing at '{FarmTerrain.TileSetPath}'.");
            var fences = (TileSetAtlasSource)_farmDonor.GetSource(FarmTerrain.FarmSource);
            if (tileSet.AddSource(fences, FenceSourceId) != FenceSourceId)
                throw new InvalidOperationException($"Roadside TileSet refused source {FenceSourceId}.");
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

    // Landscape palette, hexes straight from the town handoff's design tokens.
    private static readonly Color WaterDeep = new("2e5566");
    private static readonly Color WaterMid = new("47788c");
    private static readonly Color SkyDay = new("8fb8cf");
    private static readonly Color Ink700 = new("2b241d");
    private static readonly Color GreenDark = new("2f5228");
    private static readonly Color GreenMid = new("457539");
    private static readonly Color GreenLight = new("5f9445");

    /// <summary>
    /// The placeholder <see cref="LandscapeTiles"/> sheet: water mottle (cols 0-1), deep
    /// water (cols 2-3), and a round bush on transparency (cols 4-5) — ink-shadowed
    /// below, lit from the upper left.
    /// </summary>
    private static ImageTexture BuildLandscapeSheet()
    {
        const int size = MapRoot.TileSize;
        var img = Image.CreateEmpty(LandscapeTiles.Columns * size, size, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        for (int col = 0; col < LandscapeTiles.Columns; col++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int roll = Mottle(100 + col, x, y);
                    Color c;
                    if (col < 2)
                    {
                        c = roll < 10 ? WaterDeep : roll < 14 ? SkyDay : WaterMid;
                    }
                    else if (col < 4)
                    {
                        c = roll < 10 ? WaterMid : WaterDeep;
                    }
                    else
                    {
                        float dx = x - 7.5f, dy = y - 8.5f;
                        float d2 = dx * dx + dy * dy;
                        if (d2 > 49f)
                            continue;
                        if (d2 > 36f)
                            c = y >= 9 ? Ink700 : GreenDark;
                        else if (x + y < 15)
                            c = roll < 12 ? GreenLight : GreenMid;
                        else
                            c = roll < 30 ? GreenDark : GreenMid;
                    }
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
