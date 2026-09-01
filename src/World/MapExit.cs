using Godot;
using TheHaunt.Player;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Walk-on travel trigger. The owning map positions it, gives it a
/// CollisionShape2D covering the exit tiles, and wires the destination plus an
/// optional enable gate; Main owns the actual fade/swap flow.
/// </summary>
public partial class MapExit : Area2D
{
    public string TargetMapId { get; set; } = "";
    public string TargetSpawnId { get; set; } = "default";

    /// <summary>Set by the owning map; null = always enabled.</summary>
    public Func<bool>? IsEnabled;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1; // detects the player body
        Monitoring = true;
        Monitorable = false;

        // Godot signal — auto-disconnects on free, no manual -= needed.
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        // The PlayerHasControl gate refuses the arrival-frame BodyEntered during
        // Cutscene, and the signal never re-fires without exit + re-entry.
        if (body is PlayerController && (IsEnabled?.Invoke() ?? true)
            && GameState.Instance.PlayerHasControl)
        {
            // Where in the mouth the body crossed, so the arrival can keep the
            // player's place across the seam (MapRoot.GetArrival).
            WorldSim.Instance.RequestTravel(TargetMapId, TargetSpawnId,
                body.GlobalPosition - GlobalPosition);
        }
    }
}
