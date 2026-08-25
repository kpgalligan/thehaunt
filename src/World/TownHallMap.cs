using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Programmatic placeholder meeting hall, 40x23 tiles — authored at least the
/// viewport size so the camera clamp never engages. Plank floor, blocking wall
/// ring (doubled on the south side so the Door back to town sits flush in it),
/// podium visual at the north end. No bed, no farmland.
/// </summary>
public partial class TownHallMap : MapRoot
{
    private const int Width = 40;
    private const int Height = 23;

    // Atlas tile indices (atlas coords (i, 0)).
    private const int FloorA = 0;
    private const int FloorB = 1;
    private const int Wall = 2;
    private const int Podium = 3;
    private const int TileCount = 4;

    private const int DoorX = 20;
    private const int DoorY = 21;

    private static readonly Color[] TileColors =
    {
        new("8a6a48"), // floor plank A
        new("856544"), // floor plank B
        new("6a6a6a"), // wall
        new("9a7a4a"), // podium wood
    };

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.TownHall;
        base._EnterTree();
    }

    public override void _Ready()
    {
        var tileSet = BuildTileSet();
        BuildGround(tileSet);
        BuildObstacles(tileSet);
        BuildSpawns();
        BuildDoor();
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
            bool walkable = i is FloorA or FloorB;
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

        // Plank seams on the floor tiles; a lighter top edge on the podium.
        for (int i = FloorA; i <= FloorB; i++)
        {
            var seam = TileColors[i].Darkened(0.2f);
            img.FillRect(new Rect2I(i * TileSize, 7, TileSize, 1), seam);
            img.FillRect(new Rect2I(i * TileSize, 15, TileSize, 1), seam);
        }
        img.FillRect(new Rect2I(Podium * TileSize, 0, TileSize, 2), TileColors[Podium].Lightened(0.2f));

        return ImageTexture.CreateFromImage(img);
    }

    private void BuildGround(TileSet tileSet)
    {
        var ground = new TileMapLayer { Name = "Ground", TileSet = tileSet };
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int tile = (x * 7 + y * 13) % 2 == 0 ? FloorA : FloorB;
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
                // Ring, with the south wall doubled (rows 21-22) so the door
                // sits flush in it; the Door node fills the gap at the door cell.
                bool wall = x == 0 || x == Width - 1 || y == 0 || y >= Height - 2;
                if (wall && !(x == DoorX && y == DoorY))
                    obstacles.SetCell(new Vector2I(x, y), 0, new Vector2I(Wall, 0));
            }
        }

        // Podium visual; the mayor's staging tile (20,6) is the row in front of it.
        for (int y = 4; y <= 5; y++)
            for (int x = 19; x <= 21; x++)
                obstacles.SetCell(new Vector2I(x, y), 0, new Vector2I(Podium, 0));

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
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 19 * TileSize + 8), // (328, 312)
        });
        AddChild(spawns);
    }

    private void BuildDoor()
    {
        AddChild(new Door
        {
            Name = "TownDoor",
            TargetMapId = MapIds.Town,
            TargetSpawnId = "from_hall",
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (328, 344)
        });
    }
}
