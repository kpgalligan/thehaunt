using Godot;
using TheHaunt.World;

namespace TheHaunt.Player;

public partial class InteractionProbe : Area2D
{
    private const float FeetOffsetY = 6f;
    private const float Reach = 14f;
    private const float Radius = 8f;

    public IInteractable? Focused { get; private set; }
    public event Action<IInteractable?>? FocusChanged; // fired only on change

    public override void _Ready()
    {
        Monitoring = true;
        Monitorable = false;
        CollisionLayer = 0;
        CollisionMask = 2;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        SetFacing(0);
    }

    public override void _PhysicsProcess(double delta)
    {
        // Poll overlaps instead of tracking enter/exit signals — robust against
        // signal ordering and areas freed while overlapped.
        var interactor = GetParent() as Node2D;
        IInteractable? nearest = null;
        float nearestDistSq = float.PositiveInfinity;
        foreach (var area in GetOverlappingAreas())
        {
            // Focus only what would actually respond — a focused-but-refusing
            // candidate renders a lying prompt (silent NPCs are the live case).
            if (area is not IInteractable candidate
                || interactor == null || !candidate.CanInteract(interactor))
                continue;
            float distSq = GlobalPosition.DistanceSquaredTo(area.GlobalPosition);
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearest = candidate;
            }
        }

        if (!ReferenceEquals(nearest, Focused))
        {
            Focused = nearest;
            FocusChanged?.Invoke(Focused);
        }
    }

    public void SetFacing(int facing)
    {
        Vector2 dir = facing switch
        {
            1 => Vector2.Left,
            2 => Vector2.Right,
            3 => Vector2.Up,
            _ => Vector2.Down,
        };
        Position = new Vector2(0, FeetOffsetY) + dir * Reach;
    }

    public void TryInteract(Node2D interactor)
    {
        // Focused can point at a node freed since the last poll: despawns happen after
        // this frame's poll, and the parent player polls interact BEFORE the probe's
        // next re-poll can clear the stale reference.
        if (Focused is GodotObject obj && !IsInstanceValid(obj))
        {
            Focused = null;
            return;
        }
        if (Focused != null && Focused.CanInteract(interactor))
            Focused.Interact(interactor);
    }
}
