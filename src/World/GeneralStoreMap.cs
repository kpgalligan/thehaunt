using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Programmatic placeholder general store interior, 14x10 tiles (224x160) —
/// FarmHouseMap's shell: oversized near-black ColorRect FIRST (behind Ground)
/// so the expanded camera limits read as darkness, plank floor, blocking wall
/// ring with the Door back to town flush in the south wall. A wall-to-wall
/// counter row seals the back area y1-3, so the shopkeeper (staged at (6,3) by
/// schedule) is unreachable by construction; the ShopCounter Area2D spanning
/// the counter strip is the shop entry point. No farmland.
/// </summary>
public partial class GeneralStoreMap : MapRoot
{
    private const int Width = 14;
    private const int Height = 10;

    // Atlas tile indices (atlas coords (i, 0)).
    private const int FloorA = 0;
    private const int FloorB = 1;
    private const int Wall = 2;
    private const int Counter = 3;
    private const int TileCount = 4;

    private const int DoorX = 7;
    private const int DoorY = 9;

    private static readonly Color[] TileColors =
    {
        new("8a6a48"), // floor plank A
        new("856544"), // floor plank B
        new("6a6a6a"), // wall
        new("9a7a4a"), // counter wood
    };

    /// <summary>Indoors: fixed warm key, never the day/night tint.</summary>
    public override bool IsInterior => true;

    public override void _EnterTree()
    {
        // Default the id before registration so WorldSim never sees a nameless map.
        if (MapId.Length == 0)
            MapId = MapIds.GeneralStore;
        base._EnterTree();
    }

    public override void _Ready()
    {
        BuildSurround();
        var tileSet = BuildTileSet();
        BuildGround(tileSet);
        BuildObstacles(tileSet);
        BuildSpawns();
        BuildInteractables();
    }

    // ------------------------------------------------------------------
    // Surround / Ground / Obstacles
    // ------------------------------------------------------------------

    /// <summary>
    /// Oversized near-black backdrop, added before Ground so it draws behind
    /// everything. MapRoot.ExpandToViewport grows the camera limits to 640x360
    /// centered on the 224x160 interior; the overshoot must read as darkness,
    /// not the clear color. MouseFilter Ignore so the giant Control never
    /// swallows tool clicks.
    /// </summary>
    private void BuildSurround()
    {
        AddChild(new ColorRect
        {
            Name = "Surround",
            Color = new Color("0e0e12"),
            Position = new Vector2(-640, -360),
            Size = new Vector2(1600, 1000),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
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

        // Plank seams on the floor tiles; a lighter worked top edge on the counter.
        for (int i = FloorA; i <= FloorB; i++)
        {
            var seam = TileColors[i].Darkened(0.2f);
            img.FillRect(new Rect2I(i * TileSize, 7, TileSize, 1), seam);
            img.FillRect(new Rect2I(i * TileSize, 15, TileSize, 1), seam);
        }
        img.FillRect(new Rect2I(Counter * TileSize, 0, TileSize, 2), TileColors[Counter].Lightened(0.2f));

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
                // Single-thickness ring; the Door node fills the gap at the door cell.
                bool wall = x == 0 || x == Width - 1 || y == 0 || y == Height - 1;
                if (wall && !(x == DoorX && y == DoorY))
                    obstacles.SetCell(new Vector2I(x, y), 0, new Vector2I(Wall, 0));
            }
        }

        // Counter row, WALL-TO-WALL (x1-12 at y4, meeting the ring on both
        // sides): the back area y1-3 is sealed by construction.
        for (int x = 1; x <= 12; x++)
            obstacles.SetCell(new Vector2I(x, 4), 0, new Vector2I(Counter, 0));

        AddChild(obstacles);
    }

    // ------------------------------------------------------------------
    // Spawns / interactables
    // ------------------------------------------------------------------

    private void BuildSpawns()
    {
        var spawns = new Node2D { Name = "Spawns" };
        spawns.AddChild(new Marker2D
        {
            Name = "entry",
            Position = new Vector2(DoorX * TileSize + 8, 8 * TileSize + 8), // (120, 136)
        });
        spawns.AddChild(new Marker2D
        {
            Name = "default",
            Position = new Vector2(7 * TileSize + 8, 6 * TileSize + 8), // (120, 104)
        });
        AddChild(spawns);
    }

    private void BuildInteractables()
    {
        // ShopCounter has no sprite and no blocker of its own — the counter
        // tiles are the visual and the collision; the owning map supplies the
        // shape covering them (MapExit precedent).
        var counter = new ShopCounter
        {
            Name = "ShopCounter",
            Position = new Vector2(112, 72), // center of the counter strip x1-12, y4
        };
        counter.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(192, 16) },
        });
        AddChild(counter);

        AddChild(new Door
        {
            Name = "TownDoor",
            TargetMapId = MapIds.Town,
            TargetSpawnId = "from_store",
            Position = new Vector2(DoorX * TileSize + 8, DoorY * TileSize + 8), // (120, 152)
        });
    }
}
