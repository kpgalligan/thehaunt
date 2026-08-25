using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Programmatic placeholder town, 48x30 tiles, TestMap style. Grass with an
/// east-west dirt road on rows 14-15 (continuous with the farm road), a stone
/// town-hall facade with its Door, a plaza south of the road, and a west-edge
/// MapExit back to the farm — always enabled: leaving town is never gated.
/// No bed, no farmland (IsTillable stays base false).
/// </summary>
public partial class TownMap : MapRoot
{
    private const int Width = 48;
    private const int Height = 30;

    // Atlas tile indices (atlas coords (i, 0)).
    private const int GrassA = 0;
    private const int GrassB = 1;
    private const int GrassC = 2;
    private const int Dirt = 3;
    private const int Stone = 4; // scattered rocks + map border
    private const int Wall = 5;  // town-hall masonry
    private const int TileCount = 6;

    private const int DoorX = 23;
    private const int DoorY = 11;

    private static readonly Color[] TileColors =
    {
        new("4a7c3a"), // grass A
        new("457539"), // grass B
        new("4f823d"), // grass C
        new("8a6a45"), // dirt
        new("7a7a7a"), // stone
        new("9a9a8a"), // wall masonry
    };

    // Decorative rocks; the road rows, plaza, spawns, door approach, and the
    // frozen NPC staging tiles (24,19),(30,13),(31,16),(33,13) all stay clear.
    private static readonly Vector2I[] StoneCoords =
    {
        new(8, 6), new(15, 5), new(38, 8), new(6, 18), new(12, 22),
        new(36, 24), new(40, 20), new(29, 22),
    };

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.Town;
        base._EnterTree();
    }

    public override void _Ready()
    {
        var tileSet = BuildTileSet();
        BuildGround(tileSet);
        BuildObstacles(tileSet);
        BuildSpawns();
        BuildTravel();
    }

    // ------------------------------------------------------------------
    // Ground / Obstacles (shared TileSet with physics + walkable data)
    // ------------------------------------------------------------------

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

        // Wall gets mortar lines so the facade reads as masonry, not pavement.
        var mortar = TileColors[Wall].Darkened(0.25f);
        for (int py = 3; py < TileSize; py += 5)
            img.FillRect(new Rect2I(Wall * TileSize, py, TileSize, 1), mortar);

        return ImageTexture.CreateFromImage(img);
    }

    private void BuildGround(TileSet tileSet)
    {
        var ground = new TileMapLayer { Name = "Ground", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool road = y is 14 or 15;                             // continuous with the farm road rows
                bool plaza = x >= 22 && x <= 26 && y >= 18 && y <= 21; // town square
                int tile = road || plaza ? Dirt : (x * 7 + y * 13) % 3;
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
                bool border = x == 0 || x == Width - 1 || y == 0 || y == Height - 1;
                // The border opens at the west road mouth, where the farm exit sits.
                bool exitOpening = x == 0 && y is 14 or 15;
                if (border && !exitOpening)
                    obstacles.SetCell(new Vector2I(x, y), 0, new Vector2I(Stone, 0));
            }
        }

        // Town-hall facade block; the Door node fills the gap at the door cell.
        for (int y = 6; y <= 11; y++)
        {
            for (int x = 20; x <= 27; x++)
            {
                if (x == DoorX && y == DoorY)
                    continue;
                obstacles.SetCell(new Vector2I(x, y), 0, new Vector2I(Wall, 0));
            }
        }

        foreach (var coord in StoneCoords)
            obstacles.SetCell(coord, 0, new Vector2I(Stone, 0));
        AddChild(obstacles);
    }

    // ------------------------------------------------------------------
    // Spawns / travel
    // ------------------------------------------------------------------

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "from_farm",
            // >= 1 tile clear of the west exit area (spawn-clearance rule).
            Position = new Vector2(2 * TileSize + 8, 15 * TileSize + 8), // (40, 248)
        });
        spawns.AddChild(new Marker2D
        {
            Name = "from_hall",
            Position = new Vector2(DoorX * TileSize + 8, 13 * TileSize + 8), // (376, 224)
        });
        AddChild(spawns);
    }

    private void BuildTravel()
    {
        // West road mouth back to the farm — always enabled (IsEnabled null).
        var exit = new MapExit
        {
            Name = "FarmExit",
            TargetMapId = MapIds.Farm,
            TargetSpawnId = "road",
            Position = new Vector2(8, 15 * TileSize), // center of tiles (0,14)-(0,15)
        };
        exit.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(16, 32) },
        });
        AddChild(exit);

        AddChild(new Door
        {
            Name = "TownHallDoor",
            TargetMapId = MapIds.TownHall,
            TargetSpawnId = "entry",
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (376, 184)
        });
    }
}
