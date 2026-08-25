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

    /// <summary>
    /// Whether a saved player position on this tile is physically valid: walkable
    /// ground, no obstacle cell, no Door blocker. Main's boot guard uses this to
    /// bounce positions that new geometry (e.g. the 3b building facades) has since
    /// swallowed — the save's numbers are never rewritten, the player just respawns.
    /// </summary>
    public virtual bool IsStandable(Vector2I tile)
    {
        var ground = Ground;
        if (ground == null)
            return true; // no terrain info: don't second-guess the save
        var tileData = ground.GetCellTileData(tile);
        if (tileData == null || !tileData.GetCustomData("walkable").AsBool())
            return false;
        var obstacles = GetNodeOrNull<TileMapLayer>("Obstacles");
        if (obstacles != null && obstacles.GetCellSourceId(tile) != -1)
            return false;
        var doors = new List<Door>();
        CollectDoors(this, doors);
        foreach (Door door in doors)
        {
            var doorTile = new Vector2I(
                Mathf.FloorToInt(door.GlobalPosition.X / TileSize),
                Mathf.FloorToInt(door.GlobalPosition.Y / TileSize));
            if (doorTile == tile)
                return false; // doors carry full-tile blockers
        }
        return true;
    }

    private static void CollectDoors(Node node, List<Door> doors)
    {
        if (node is Door door)
            doors.Add(door);
        foreach (Node child in node.GetChildren())
            CollectDoors(child, doors);
    }

    // Terrain-only tillability; existing tile records are the model's concern.
    public virtual bool IsTillable(int x, int y) => false;

    // O(1) incremental visual update for one tile's record (null = no record).
    public virtual void RefreshTile(int x, int y, TileRecord? record) { }

    // Hydrate visuals from the model after instancing. No-op in the base.
    public virtual void ApplyState(MapState state) { }
}
