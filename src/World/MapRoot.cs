using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

public partial class MapRoot : Node2D
{
    public const int TileSize = 16;

    // 480x270 shows 30x17 tiles — roughly double the apparent size of every character
    // and building against the old 640x360 (art bible §01). Kept in sync with
    // display/window/size in project.godot.
    public const int ViewportWidth = 480;
    public const int ViewportHeight = 270;

    [Export] public string MapId { get; set; } = "";

    /// <summary>The recipe source a map reports when it built from a handed-in <see cref="RecipeOverride"/>.</summary>
    public const string EditedRecipe = "<editor, unsaved>";

    /// <summary>
    /// A <see cref="MapRecipe"/> to build from INSTEAD of reading this map's own file.
    /// The placement editor sets it before the node enters the tree, because the stage
    /// holds the single mutable copy of the placements in memory and a rebuild-per-drag
    /// has to show the drag that is not on disk yet — re-reading the file mid-drag would
    /// undo the edit the rebuild was for.
    ///
    /// Null everywhere else. The running game never sets it, so a map with no override
    /// reads data/maps/(id).json exactly as it always has. Not save state and not
    /// durable: it lives for one node's lifetime, and the stage throws that node away on
    /// the next rebuild.
    /// </summary>
    public MapRecipe? RecipeOverride { get; set; }

    public TileMapLayer? Ground => GetNodeOrNull<TileMapLayer>("Ground");

    /// <summary>
    /// Interiors take a fixed warm key instead of the day/night tint — that contrast
    /// is what makes stepping inside at dusk feel like relief (art handoff §6).
    /// </summary>
    public virtual bool IsInterior => false;

    public override void _EnterTree()
    {
        // Merges this map's sprites into the Y-sort Main runs over World: the player
        // passes behind a roof overhang or a prop and in front of its base row.
        YSortEnabled = true;
        WorldSim.Instance.RegisterMap(this);
    }

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
        return new Rect2(0, 0, ViewportWidth, ViewportHeight);
    }

    // A map smaller than the viewport makes Camera2D limits unsatisfiable and pins the
    // view asymmetrically; grow to at least the viewport, centered on the original rect.
    private static Rect2 ExpandToViewport(Rect2 rect)
    {
        var size = new Vector2(
            Mathf.Max(rect.Size.X, ViewportWidth), Mathf.Max(rect.Size.Y, ViewportHeight));
        return new Rect2(rect.Position - (size - rect.Size) / 2f, size);
    }

    public Vector2 GetSpawn(string id = "default")
    {
        var marker = GetNodeOrNull<Marker2D>($"Spawns/{id}");
        return marker?.GlobalPosition ?? GetCameraLimits().GetCenter();
    }

    /// <summary>
    /// Where a traveller actually lands: the spawn marker, adjusted so a player who
    /// crossed a road mouth off-centre comes out the far side in the same lane.
    /// <paramref name="exitOffset"/> is their position relative to the exit zone they
    /// walked into; it is re-applied along this map's matching mouth — the exit zone
    /// standing nearest the spawn marker — on that zone's LONG axis only (the axis
    /// the mouth spans; the other axis stays the marker's, one safe tile inside the
    /// map), clamped inside the zone. A smaller mouth than the one they left just
    /// pins them to its edge. Zero offset (doors, scripted travel, tests) and maps
    /// with no exit near the marker land exactly on the marker, as ever.
    /// </summary>
    public Vector2 GetArrival(string spawnId, Vector2 exitOffset)
    {
        Vector2 spawn = GetSpawn(spawnId);
        if (exitOffset == Vector2.Zero)
            return spawn;
        if (EntryZoneNear(spawn) is not { } mouth || mouth.Size.X == mouth.Size.Y)
            return spawn;

        // Keep the feet comfortably inside the mouth's tiles.
        const float margin = 8f;
        if (mouth.Size.Y > mouth.Size.X)
        {
            spawn.Y = Mathf.Clamp(mouth.GetCenter().Y + exitOffset.Y,
                mouth.Position.Y + margin, mouth.End.Y - margin);
        }
        else
        {
            spawn.X = Mathf.Clamp(mouth.GetCenter().X + exitOffset.X,
                mouth.Position.X + margin, mouth.End.X - margin);
        }
        return spawn;
    }

    /// <summary>The rect of the exit zone standing nearest <paramref name="point"/>,
    /// if one stands within four tiles of it. Distance is measured to the zone's
    /// nearest EDGE, not its centre: an arrival marker sits a safe row or two inside
    /// the map while its mouth's rect reaches the border, and centre distance grows
    /// with the mouth's depth — the farm's road marker is three clear rows from its
    /// mouth and taught this the hard way. Every marker is far (>6 tiles) from any
    /// zone that is not its own, which is what makes proximity pairing safe.</summary>
    private Rect2? EntryZoneNear(Vector2 point)
    {
        Rect2? nearest = null;
        float best = 4 * TileSize;
        var exits = new List<MapExit>();
        CollectExits(this, exits);
        foreach (MapExit exit in exits)
        {
            foreach (Node child in exit.GetChildren())
            {
                if (child is CollisionShape2D { Shape: RectangleShape2D rect } shape)
                {
                    var zone = new Rect2(
                        exit.GlobalPosition + shape.Position - rect.Size / 2f, rect.Size);
                    var edge = new Vector2(
                        Mathf.Clamp(point.X, zone.Position.X, zone.End.X),
                        Mathf.Clamp(point.Y, zone.Position.Y, zone.End.Y));
                    float distance = point.DistanceTo(edge);
                    if (distance < best)
                    {
                        best = distance;
                        nearest = zone;
                    }
                }
            }
        }
        return nearest;
    }

    private static void CollectExits(Node node, List<MapExit> exits)
    {
        if (node is MapExit exit)
            exits.Add(exit);
        foreach (Node child in node.GetChildren())
            CollectExits(child, exits);
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

        List<(string RoleId, Vector2 Slot)>? staged = null;
        foreach (var (def, placement) in forThisMap)
        {
            if (!_npcViews.TryGetValue(def.Id, out NpcView? view))
            {
                view = new NpcView
                {
                    RoleId = def.Id,
                    SheetPath = def.SpriteSheet,
                    SheetBlock = def.SpriteBlock,
                };
                _npcViews[def.Id] = view;
                AddChild(view);
            }
            // SetAnchor, not Position: a re-stated anchor must not yank a
            // wandering view home on every ten-minute resync.
            var slot = new Vector2(
                placement.TileX * TileSize + 8, placement.TileY * TileSize + 8);
            if (view.SetAnchor(slot, placement.Facing, placement.Ambit))
                (staged ??= new()).Add((def.Id, slot));
        }

        // A slot change can stage somebody onto a tile an ambler had wandered to
        // (the pre-step probe cannot veto the future). The schedule's slot wins:
        // the ambler goes home, which is the one tile that is always theirs.
        if (staged != null)
        {
            foreach (NpcView view in _npcViews.Values)
            {
                if (!view.IsAmbling)
                    continue;
                foreach ((string roleId, Vector2 slot) in staged)
                {
                    if (roleId != view.RoleId && view.AmblePosition.DistanceTo(slot) < TileSize)
                    {
                        view.ReturnToAnchor();
                        break;
                    }
                }
            }
        }
    }

    public NpcView? GetNpcView(string roleId) => _npcViews.GetValueOrDefault(roleId);

    private readonly Dictionary<string, NpcView> _npcViews = new();

    /// <summary>
    /// Diffs the parked scooter into (at most) one child <see cref="Scooter"/> view.
    /// Null means no scooter here — not on this map, or under the player. The view is
    /// freed and respawned rather than moved when the record changes: a fresh node
    /// re-runs the blocker warm-up, which is exactly what a re-park needs.
    /// </summary>
    public void SyncScooter(ScooterData? record)
    {
        bool valid = _scooterView != null && IsInstanceValid(_scooterView)
            && !_scooterView.IsQueuedForDeletion();
        if (record == null)
        {
            if (valid)
                _scooterView!.QueueFree();
            _scooterView = null;
            return;
        }

        var position = new Vector2(
            record.TileX * TileSize + 8, record.TileY * TileSize + 8);
        if (valid && _scooterView!.Position == position)
        {
            _scooterView.ApplyFacing(record.Facing);
            return;
        }

        if (valid)
            _scooterView!.QueueFree();
        _scooterView = new Scooter { Name = "Scooter", ParkedFacing = record.Facing, Position = position };
        AddChild(_scooterView);
    }

    private Scooter? _scooterView;

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

    /// <summary>
    /// Deterministic per-cell scatter, shared by every map that picks between tile
    /// variants: the same map paints identically on every load, and no two runs disagree
    /// about where the clover is.
    /// </summary>
    protected static int Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7fffffff;
        }
    }

    protected static Vector2I Pick(Vector2I[] variants, int x, int y) =>
        variants[Hash(x, y) % variants.Length];

    // Terrain-only tillability; existing tile records are the model's concern.
    public virtual bool IsTillable(int x, int y) => false;

    /// <summary>
    /// Cells this map keeps off the hoe for a reason no tile can show: under a roof
    /// overhang, in a doorway, along the road corridor. <see cref="IsTillable"/> consults
    /// the set itself — this only hands it out.
    ///
    /// EDITOR ONLY. Nothing in the game calls it, and nothing should: a reservation is
    /// the one piece of map geometry that is completely invisible until someone tries to
    /// till it and cannot, which makes it exactly what a placement editor has to be able
    /// to draw. Empty in the base, because a map with no reservations has nothing to say.
    /// </summary>
    public virtual IReadOnlyCollection<Vector2I> ReservedTiles() => Array.Empty<Vector2I>();

    // O(1) incremental visual update for one tile's record (null = no record).
    public virtual void RefreshTile(int x, int y, TileRecord? record) { }

    // Hydrate visuals from the model after instancing. No-op in the base.
    public virtual void ApplyState(MapState state) { }
}
