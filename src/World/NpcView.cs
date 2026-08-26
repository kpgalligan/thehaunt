using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Pure NPC view: presence, position, and facing are a function of
/// (StoryFlags, GameTime), diffed into place by <see cref="MapRoot.SyncNpcs"/>.
/// Subscribes to nothing, owns no durable state — reload reconstructs it
/// bit-identically from the model.
/// </summary>
public partial class NpcView : Area2D, IInteractable
{
    public string RoleId { get; init; } = "";
    public Color Tunic { get; init; } = new("4a6ab0");

    public string PromptText => "Talk";

    private CharacterSprite? _sprite;
    private int _facing; // 0=down 1=left 2=right 3=up

    // A null selector result = present-but-silent (no Talk prompt). The
    // IsQueuedForDeletion guard closes the one-frame freed-but-overlapped
    // probe window after a despawn.
    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.PlayerHasControl && !IsQueuedForDeletion()
        && DialogueSelector.ForNpc(RoleId, SaveService.Instance.Current, Clock.Instance.Now) != null;

    public void Interact(Node2D interactor) =>
        WorldSim.Instance.StartNpcDialogue(RoleId);

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 0;
        Monitorable = true;

        _sprite = new CharacterSprite { Tunic = Tunic };
        AddChild(_sprite);
        _sprite.SetFacing(_facing);

        AddChild(new CollisionShape2D
        {
            Position = new Vector2(0, -3), // talk area aligned with the sprite
            Shape = new RectangleShape2D { Size = new Vector2(16, 22) },
        });

        // Solid blocker so NPCs stop movement like the Bed (Area2Ds don't
        // collide with bodies); footprint matches the player's feet collider.
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Position = new Vector2(0, 6),
            Shape = new RectangleShape2D { Size = new Vector2(12, 8) },
        });
        AddChild(blocker);
    }

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
