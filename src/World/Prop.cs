using Godot;

namespace TheHaunt.World;

/// <summary>
/// A base-anchored sprite cut from an atlas: plaza dressing, building facades, the
/// pieces the flat-top three-quarter projection draws in elevation. Its
/// <see cref="Node2D.Position"/> is the BOTTOM-CENTRE of its footprint, which is both
/// what the art expects (every source rect is drawn feet-down) and what Y-sorting
/// needs to decide whether the player is in front of it or behind it.
/// </summary>
public partial class Prop : Sprite2D
{
    /// <summary>Atlas source rect, in pixels.</summary>
    public Rect2 Source { get; init; }

    /// <summary>Texture the source rect is cut from.</summary>
    public string TexturePath { get; init; } = "";

    public override void _Ready()
    {
        var atlas = GD.Load<Texture2D>(TexturePath)
            ?? throw new InvalidOperationException($"Prop texture missing at '{TexturePath}'.");
        Texture = atlas;
        RegionEnabled = true;
        RegionRect = Source;
        Offset = new Vector2(0, -Source.Size.Y / 2f);
    }

    /// <summary>Bottom-centre of a footprint spanning <paramref name="tiles"/> tiles from (x, y).</summary>
    public static Vector2 Anchor(int x, int y, int tiles = 1) =>
        new(x * MapRoot.TileSize + tiles * MapRoot.TileSize / 2f, (y + 1) * MapRoot.TileSize);

    /// <summary>
    /// A plain centred sprite cut from an atlas, for the interactables (bed, chest,
    /// shipping bin) that own their collision and only needed a picture. They keep their
    /// procedural placeholder for the case this returns nothing to draw — a new map still
    /// ships before its art exists.
    /// </summary>
    public static Sprite2D Cut(string texturePath, Rect2 source, Vector2 offset = default)
    {
        var atlas = GD.Load<Texture2D>(texturePath)
            ?? throw new InvalidOperationException($"Sprite sheet missing at '{texturePath}'.");
        return new Sprite2D
        {
            Texture = atlas,
            RegionEnabled = true,
            RegionRect = source,
            Offset = offset,
        };
    }
}
