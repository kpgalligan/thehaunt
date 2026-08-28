using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Pure NPC view: presence, ANCHOR position, and facing are a function of
/// (StoryFlags, GameTime), diffed into place by <see cref="MapRoot.SyncNpcs"/>.
/// Subscribes to nothing, owns no durable state — reload reconstructs it
/// bit-identically from the model (at its anchor).
///
/// Around that anchor the view ambles on its own when the placement grants an
/// Ambit: pick a nearby standable, unoccupied tile, walk to it, stand a while,
/// repeat — the putter of someone doing their job, not pathing. All of it is
/// VOLATILE view state (like the player's pixel position between saves), gated
/// on ClockRuns so cutscenes, dialogue and menus freeze it, and bounded by the
/// anchor so the ten-minute resync never needs to yank anyone home — a sync that
/// re-states the same anchor leaves the amble alone (<see cref="SetAnchor"/>).
/// </summary>
public partial class NpcView : Area2D, IInteractable
{
    public string RoleId { get; init; } = "";
    public string SheetPath { get; init; } = CharacterSprites.SheetPath;
    public int SheetBlock { get; init; }

    public string PromptText => "Talk";

    private CharacterSprite? _sprite;
    private StaticBody2D? _blocker;
    private int _facing; // 0=down 1=left 2=right 3=up

    // The amble: anchor + radius from the schedule, one step underway at a time.
    private const float AmbleSpeed = 26f;         // px/sec — a putter, not a patrol
    private const float IdleMinSeconds = 1.6f, IdleMaxSeconds = 4.5f;
    private Vector2 _anchor;
    private int _anchorFacing;
    private int _ambit;
    private bool _anchored;
    private Vector2? _stepTarget;
    private float _idleWait;
    private RectangleShape2D? _stepProbe;

    // A null selector result = present-but-silent (no Talk prompt). The
    // IsQueuedForDeletion guard closes the one-frame freed-but-overlapped
    // probe window after a despawn.
    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.PlayerHasControl && !IsQueuedForDeletion()
        && DialogueSelector.ForNpc(RoleId, SaveService.Instance.Current, Clock.Instance.Now) != null;

    public void Interact(Node2D interactor)
    {
        // Turn to the person talking; the amble freezes on its own (ClockRuns) and
        // re-faces its own way when the walk resumes.
        FaceToward(interactor.GlobalPosition);
        WorldSim.Instance.StartNpcDialogue(RoleId);
    }

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 0;
        Monitorable = true;

        _sprite = new CharacterSprite { SheetPath = SheetPath, SheetBlock = SheetBlock };
        AddChild(_sprite);
        _sprite.SetFacing(_facing);

        AddChild(new CollisionShape2D
        {
            Position = new Vector2(0, -3), // talk area aligned with the sprite
            Shape = new RectangleShape2D { Size = new Vector2(16, 22) },
        });

        // Solid blocker so NPCs stop movement like the Bed (Area2Ds don't
        // collide with bodies); footprint matches the player's feet collider.
        _blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        _blocker.AddChild(new CollisionShape2D
        {
            Position = new Vector2(0, 6),
            Shape = new RectangleShape2D { Size = new Vector2(12, 8) },
        });
        AddChild(_blocker);
    }

    /// <summary>
    /// The scheduled staging, pushed by <see cref="MapRoot.SyncNpcs"/> on every
    /// resync. A CHANGED anchor is a slot change and teleports (static staging, as
    /// ever); the same anchor re-stated leaves the amble exactly where it was —
    /// without this, every ten-minute tick would snap a wanderer home. Returns true
    /// when it teleported, so the sync can shoo an ambler off a freshly staged slot.
    /// </summary>
    public bool SetAnchor(Vector2 position, int facing, int ambit)
    {
        if (_anchored && _anchor == position && _ambit == ambit)
        {
            if (_ambit == 0)
                SetFacing(facing);   // a fixture may still be re-aimed
            return false;
        }
        _anchored = true;
        _anchor = position;
        _anchorFacing = facing;
        _ambit = ambit;
        Position = position;
        SetFacing(facing);
        _stepTarget = null;
        _idleWait = NextIdleWait();
        _sprite?.SetMoving(false);
        return true;
    }

    /// <summary>Snap the amble home — the sync's answer when a slot change stages
    /// somebody onto the tile this view had wandered to (the schedule's slot wins).</summary>
    public void ReturnToAnchor()
    {
        if (!_anchored)
            return;
        Position = _anchor;
        SetFacing(_anchorFacing);
        _stepTarget = null;
        _idleWait = NextIdleWait();
        _sprite?.SetMoving(false);
    }

    /// <summary>Where the amble currently stands, snapped to the grid.</summary>
    public Vector2 AmblePosition => Position;

    /// <summary>Whether this view ambles at all (Ambit &gt; 0) and has left its anchor.</summary>
    public bool IsAmbling => _ambit > 0 && _anchored && Position != _anchor;

    public override void _PhysicsProcess(double delta)
    {
        if (_ambit <= 0 || !_anchored || IsQueuedForDeletion())
            return;
        if (!GameState.Instance.ClockRuns)
        {
            _sprite?.SetMoving(false);   // freeze mid-step; the walk resumes with the clock
            return;
        }

        if (_stepTarget is { } target)
        {
            _sprite?.SetMoving(true);
            float step = AmbleSpeed * (float)delta;
            Vector2 to = target - Position;
            if (to.Length() <= step)
            {
                // Two amblers can pick the same free tile in the same window; the
                // pre-step probe cannot see a step still in flight. Re-check on the
                // doorstep and yield — home is the one tile that is always theirs.
                if (Occupied(target))
                {
                    ReturnToAnchor();
                    return;
                }
                Position = target;
                _stepTarget = null;
                _idleWait = NextIdleWait();
                _sprite?.SetMoving(false);
                if (Position == _anchor)
                    SetFacing(_anchorFacing);   // back at the post, back to the posture
            }
            else
            {
                FaceToward(target);
                Position += to.Normalized() * step;
            }
            return;
        }

        _idleWait -= (float)delta;
        if (_idleWait > 0f)
            return;
        _idleWait = NextIdleWait();
        TryStartStep();
    }

    /// <summary>One adjacent tile, chosen at random from the directions that stay
    /// within the ambit of the anchor, on standable ground, with nobody on them.</summary>
    private void TryStartStep()
    {
        if (GetParent() is not MapRoot map)
            return;
        var here = new Vector2I(
            Mathf.FloorToInt(Position.X / MapRoot.TileSize),
            Mathf.FloorToInt(Position.Y / MapRoot.TileSize));
        var anchorTile = new Vector2I(
            Mathf.FloorToInt(_anchor.X / MapRoot.TileSize),
            Mathf.FloorToInt(_anchor.Y / MapRoot.TileSize));

        Span<int> order = stackalloc int[] { 0, 1, 2, 3 };
        for (int i = 3; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        foreach (int direction in order)
        {
            Vector2I tile = here + direction switch
            {
                0 => Vector2I.Down, 1 => Vector2I.Left, 2 => Vector2I.Right, _ => Vector2I.Up,
            };
            if (Math.Abs(tile.X - anchorTile.X) > _ambit || Math.Abs(tile.Y - anchorTile.Y) > _ambit)
                continue;
            if (!map.IsStandable(tile))
                continue;
            var centre = new Vector2(
                tile.X * MapRoot.TileSize + 8, tile.Y * MapRoot.TileSize + 8);
            if (Occupied(centre))
                continue;
            _stepTarget = centre;
            FaceToward(centre);
            _sprite?.SetMoving(true);
            return;
        }
    }

    /// <summary>Anything solid already standing on the tile: the player's body, another
    /// NPC's blocker, the parked scooter — the bodies IsStandable cannot see.</summary>
    private bool Occupied(Vector2 tileCentre)
    {
        if (_blocker == null)
            return false;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = _stepProbe ??= new RectangleShape2D { Size = new Vector2(12, 12) },
            Transform = new Transform2D(0, tileCentre),
            CollisionMask = 1,
            CollideWithAreas = false,
            Exclude = new Godot.Collections.Array<Rid> { _blocker.GetRid() },
        };
        return GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count > 0;
    }

    private void FaceToward(Vector2 point)
    {
        Vector2 to = point - GlobalPosition;
        SetFacing(Mathf.Abs(to.X) >= Mathf.Abs(to.Y)
            ? (to.X < 0 ? 1 : 2)
            : (to.Y < 0 ? 3 : 0));
    }

    private static float NextIdleWait() =>
        IdleMinSeconds + Random.Shared.NextSingle() * (IdleMaxSeconds - IdleMinSeconds);

    /// <summary>Clamps and applies facing; swaps the texture only on change.</summary>
    public void SetFacing(int facing)
    {
        facing = Math.Clamp(facing, 0, 3);
        if (facing == _facing)
            return;
        _facing = facing;
        _sprite?.SetFacing(_facing);
    }
}
