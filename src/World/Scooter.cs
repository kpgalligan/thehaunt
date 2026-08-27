using Godot;
using TheHaunt.Core;
using TheHaunt.Player;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The parked scooter as a world object (docs/designs/design_handoff_scooter). A VIEW:
/// the model record lives in GameData.Scooter and <see cref="MapRoot.SyncScooter"/>
/// spawns/frees this node from it — the node itself holds nothing durable. Interacting
/// mounts through the bus (WorldSim.MountScooter) and steps the player onto the deck;
/// no cutscene, no fade (handoff: mounting has no ceremony).
///
/// The 16x32 cell stands on its tile and overhangs the row above, like furniture; which
/// of the three parked views draws follows the facing the rider dismounted with.
/// </summary>
public partial class Scooter : Area2D, IInteractable
{
    public const string SheetPath = "res://assets/sprites/scooter_parked.png";

    private const int CellWidth = 16;
    private const int CellHeight = 32;

    private Sprite2D? _sprite;
    private CollisionShape2D? _blockerShape;
    private int _physicsFrames;

    /// <summary>Player facing at dismount (0-3); picks the parked cell. Set by the sync.</summary>
    public int ParkedFacing { get; set; } = ScooterRules.HomeFacing;

    public string PromptText => "Ride";

    public bool CanInteract(Node2D interactor) => GameState.Instance.PlayerHasControl;

    public void Interact(Node2D interactor)
    {
        // Step onto the deck: the player snaps to the scooter's tile so the object
        // never teleports to the rider. Position is node-owned volatile state, so the
        // node moves it — the model write is the bus's. SKIP the snap when the tile
        // is no longer standable (map geometry drifted under an old save) — snapping
        // would wedge the rider in collision; the mount itself is still fine.
        if (WorldSim.Instance.MountScooter() && interactor is Node2D rider)
        {
            var tile = new Vector2I(
                Mathf.FloorToInt(GlobalPosition.X / MapRoot.TileSize),
                Mathf.FloorToInt(GlobalPosition.Y / MapRoot.TileSize));
            if (GetParent() is not MapRoot map || map.IsStandable(tile))
            {
                rider.GlobalPosition = GlobalPosition;
            }
        }
    }

    public override void _Ready()
    {
        CollisionLayer = 2;   // interactable — the probe looks here
        CollisionMask = 1;    // and it watches the player body, to arm the blocker
        Monitorable = true;
        Monitoring = true;

        _sprite = new Sprite2D
        {
            Texture = GD.Load<Texture2D>(SheetPath)
                ?? throw new InvalidOperationException($"Parked scooter sheet missing at '{SheetPath}'."),
            RegionEnabled = true,
            Offset = new Vector2(0, (MapRoot.TileSize - CellHeight) / 2f),
        };
        AddChild(_sprite);
        ApplyFacing(ParkedFacing);

        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(16, 16) },
        });

        // Solid blocker so the parked scooter stops movement (Chest pattern) — but a
        // dismount drops it ON the player's tile, and enabling a static body around a
        // body inside it wedges the physics. The shape starts disabled and arms in
        // _PhysicsProcess once no player overlaps the cell.
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        _blockerShape = new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(12, 12) },
            Disabled = true,
        };
        blocker.AddChild(_blockerShape);
        AddChild(blocker);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_blockerShape == null || !_blockerShape.Disabled)
        {
            SetPhysicsProcess(false);   // armed — nothing left to poll
            return;
        }
        // A fresh Area2D reports no overlaps until the physics server has run a step
        // with it; trusting frame one would arm the blocker around the dismounting
        // player's feet. Wait out the warm-up.
        if (_physicsFrames++ < 2)
        {
            return;
        }
        foreach (Node2D body in GetOverlappingBodies())
        {
            if (body is PlayerController)
            {
                return;   // still standing over it
            }
        }
        _blockerShape.Disabled = false;
        SetPhysicsProcess(false);
    }

    /// <summary>Re-aim an existing view (the sync reuses nodes when only facing moved).</summary>
    public void ApplyFacing(int facing)
    {
        ParkedFacing = Math.Clamp(facing, 0, 3);
        if (_sprite == null)
        {
            return;
        }
        _sprite.RegionRect = new Rect2(
            ScooterRules.ParkedColumn(ParkedFacing) * CellWidth, 0, CellWidth, CellHeight);
        _sprite.FlipH = ScooterRules.ParkedFlipH(ParkedFacing);
    }
}
