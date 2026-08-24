using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

public partial class MapRoot : Node2D
{
    public const int TileSize = 16;

    [Export] public string MapId { get; set; } = "";

    public TileMapLayer? Ground => GetNodeOrNull<TileMapLayer>("Ground");

    public virtual Rect2 GetCameraLimits()
    {
        var ground = Ground;
        if (ground != null)
        {
            Rect2I used = ground.GetUsedRect();
            if (used.Size.X > 0 && used.Size.Y > 0)
                return new Rect2(used.Position * TileSize, used.Size * TileSize);
        }
        return new Rect2(0, 0, 640, 360);
    }

    public Vector2 GetSpawn(string id = "default")
    {
        var marker = GetNodeOrNull<Marker2D>($"Spawns/{id}");
        return marker?.GlobalPosition ?? GetCameraLimits().GetCenter();
    }

    // Hydrate visuals from the model after instancing. No-op in the foundation.
    public virtual void ApplyState(MapState state) { }
}
