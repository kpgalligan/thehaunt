using Godot;

namespace TheHaunt.World;

/// <summary>
/// Programmatic placeholder map, 40x30 tiles. Builds its TileSet, layers, spawn
/// markers, and interactables entirely in _Ready — no scene file, no imported assets.
/// </summary>
public partial class TestMap : MapRoot
{
    private const int Width = 40;
    private const int Height = 30;
    private const int BorderThickness = 2;

    // Atlas tile indices (atlas coords (i, 0)).
    private const int GrassA = 0;
    private const int GrassB = 1;
    private const int GrassC = 2;
    private const int Dirt = 3;
    private const int Water = 4;
    private const int Stone = 5;
    private const int TileCount = 6;

    private static readonly Color[] TileColors =
    {
        new("4a7c3a"), // grass A
        new("457539"), // grass B
        new("4f823d"), // grass C
        new("8a6a45"), // dirt
        new("3a6ea5"), // water
        new("7a7a7a"), // stone
    };

    private static readonly Vector2I[] StoneCoords =
    {
        new(5, 5), new(15, 20), new(30, 10), new(25, 22), new(10, 18), new(33, 25),
        new(18, 6), new(28, 15), new(6, 24), new(35, 7), new(22, 3), new(13, 13),
    };

    public override void _Ready()
    {
        if (MapId.Length == 0)
            MapId = "test_farm";

        var tileSet = BuildTileSet();
        BuildGround(tileSet);
        BuildObstacles(tileSet);
        BuildSpawns();
        BuildInteractables();
    }

    private static TileSet BuildTileSet()
    {
        var ts = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        ts.AddPhysicsLayer();                        // index 0
        ts.SetPhysicsLayerCollisionLayer(0, 1);      // world layer
        ts.SetPhysicsLayerCollisionMask(0, 0);
        ts.AddCustomDataLayer();                     // index 0
        ts.SetCustomDataLayerName(0, "walkable");
        ts.SetCustomDataLayerType(0, Variant.Type.Bool);

        var src = new TileSetAtlasSource
        {
            Texture = BuildAtlasTexture(),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };
        ts.AddSource(src, 0);

        for (int i = 0; i < TileCount; i++)
        {
            var coords = new Vector2I(i, 0);
            src.CreateTile(coords);
            var td = src.GetTileData(coords, 0);
            bool walkable = i is GrassA or GrassB or GrassC or Dirt;
            td.SetCustomData("walkable", walkable);
            if (!walkable)
            {
                td.SetCollisionPolygonsCount(0, 1);
                td.SetCollisionPolygonPoints(0, 0, new[]
                {
                    new Vector2(-8, -8), new Vector2(8, -8), new Vector2(8, 8), new Vector2(-8, 8),
                });
            }
        }

        return ts;
    }

    private static ImageTexture BuildAtlasTexture()
    {
        var img = Image.CreateEmpty(TileCount * TileSize, TileSize, false, Image.Format.Rgba8);
        for (int i = 0; i < TileCount; i++)
        {
            var baseColor = TileColors[i];
            var dark = baseColor.Darkened(0.15f);
            var light = baseColor.Lightened(0.1f);
            for (int py = 0; py < TileSize; py++)
            {
                for (int px = 0; px < TileSize; px++)
                {
                    // Coordinate hash sprinkles a few darker/lighter speckles per tile.
                    int hash = (px * 31 + py * 17 + i * 7) % 23;
                    var color = hash == 0 ? dark : hash == 1 ? light : baseColor;
                    img.SetPixel(i * TileSize + px, py, color);
                }
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    private void BuildGround(TileSet tileSet)
    {
        var ground = new TileMapLayer { Name = "Ground", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool dirt = x >= 18 && x <= 24 && y >= 12 && y <= 17;
                int tile = dirt ? Dirt : (x * 7 + y * 13) % 3;
                ground.SetCell(new Vector2I(x, y), 0, new Vector2I(tile, 0));
            }
        }
        AddChild(ground);
    }

    private void BuildObstacles(TileSet tileSet)
    {
        var obstacles = new TileMapLayer { Name = "Obstacles", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool border = x < BorderThickness || x >= Width - BorderThickness
                    || y < BorderThickness || y >= Height - BorderThickness;
                if (border)
                    obstacles.SetCell(new Vector2I(x, y), 0, new Vector2I(Water, 0));
            }
        }
        foreach (var coord in StoneCoords)
            obstacles.SetCell(coord, 0, new Vector2I(Stone, 0));
        AddChild(obstacles);
    }

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "default",
            Position = new Vector2(20 * TileSize + 8, 15 * TileSize + 8), // (328, 248)
        });
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        var interactables = new Node2D { Name = "Interactables" };
        interactables.AddChild(new Bed
        {
            Name = "Bed",
            Position = new Vector2(136, 152), // footprint tiles (8,8)-(8,9), position per spec
        });
        interactables.AddChild(new Sign
        {
            Name = "Sign",
            Position = new Vector2(200, 136), // tile (12,8) center
            Message = "Placeholder sign. Real text comes later.",
        });
        AddChild(interactables);
    }
}
