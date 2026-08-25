using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

public partial class MapRoot : Node2D
{
    public const int TileSize = 16;

    [Export] public string MapId { get; set; } = "";

    public TileMapLayer? Ground => GetNodeOrNull<TileMapLayer>("Ground");

    public override void _EnterTree() => WorldSim.Instance.RegisterMap(this);

    public override void _ExitTree() => WorldSim.Instance.UnregisterMap(this);

    public virtual Rect2 GetCameraLimits()
    {
        var ground = Ground;
        if (ground != null)
        {
            Rect2I used = ground.GetUsedRect();
            if (used.Size.X > 0 && used.Size.Y > 0)
                return ExpandToViewport(new Rect2(used.Position * TileSize, used.Size * TileSize));
        }
        return new Rect2(0, 0, 640, 360);
    }

    // An interior smaller than the viewport makes Camera2D limits unsatisfiable
    // and pins the view asymmetrically; grow to at least 640x360, centered on
    // the original rect.
    private static Rect2 ExpandToViewport(Rect2 rect)
    {
        var size = new Vector2(Mathf.Max(rect.Size.X, 640), Mathf.Max(rect.Size.Y, 360));
        return new Rect2(rect.Position - (size - rect.Size) / 2f, size);
    }

    public Vector2 GetSpawn(string id = "default")
    {
        var marker = GetNodeOrNull<Marker2D>($"Spawns/{id}");
        return marker?.GlobalPosition ?? GetCameraLimits().GetCenter();
    }

    /// <summary>
    /// Diffs the scheduled NPC set for this map into child NpcViews. Spawns
    /// missing views at tile center, moves/refaces existing ones, QueueFrees
    /// departed ones — removing them from the dict immediately, because the
    /// node lingers to end of frame and a same-frame re-add needs a fresh view.
    /// </summary>
    public void SyncNpcs(IReadOnlyList<(NpcDef Def, NpcPlacement Placement)> forThisMap)
    {
        List<string>? departed = null;
        foreach (string roleId in _npcViews.Keys)
        {
            bool present = false;
            for (int i = 0; i < forThisMap.Count; i++)
            {
                if (forThisMap[i].Def.Id == roleId)
                {
                    present = true;
                    break;
                }
            }
            if (!present)
                (departed ??= new()).Add(roleId);
        }
        if (departed != null)
        {
            foreach (string roleId in departed)
            {
                _npcViews[roleId].QueueFree();
                _npcViews.Remove(roleId);
            }
        }

        foreach (var (def, placement) in forThisMap)
        {
            if (!_npcViews.TryGetValue(def.Id, out NpcView? view))
            {
                view = new NpcView { RoleId = def.Id, Tunic = new Color(def.BodyColor) };
                _npcViews[def.Id] = view;
                AddChild(view);
            }
            view.Position = new Vector2(
                placement.TileX * TileSize + 8, placement.TileY * TileSize + 8);
            view.SetFacing(placement.Facing);
        }
    }

    public NpcView? GetNpcView(string roleId) => _npcViews.GetValueOrDefault(roleId);

    private readonly Dictionary<string, NpcView> _npcViews = new();

    // Terrain-only tillability; existing tile records are the model's concern.
    public virtual bool IsTillable(int x, int y) => false;

    // O(1) incremental visual update for one tile's record (null = no record).
    public virtual void RefreshTile(int x, int y, TileRecord? record) { }

    // Hydrate visuals from the model after instancing. No-op in the base.
    public virtual void ApplyState(MapState state) { }
}
