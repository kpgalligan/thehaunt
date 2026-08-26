using Godot;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Interactable doorway. Interacting requests travel through the bus; Main owns
/// the actual fade/swap flow. Sits flush in a wall gap — its own blocker seals
/// the gap so travel only ever happens via the interact press.
/// </summary>
public partial class Door : Area2D, IInteractable
{
    [Export] public string TargetMapId { get; set; } = "";
    [Export] public string TargetSpawnId { get; set; } = "default";

    /// <summary>
    /// False where the doorway is already drawn into the map's art (the town facades):
    /// the node still carries the blocker and the prompt, it just draws nothing.
    /// </summary>
    [Export] public bool DrawPlaceholder { get; set; } = true;

    public string PromptText => "Enter";

    // The IsQueuedForDeletion guard closes the one-frame freed-but-overlapped
    // probe window during a map swap.
    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.PlayerHasControl && !IsQueuedForDeletion();

    public void Interact(Node2D interactor) =>
        WorldSim.Instance.RequestTravel(TargetMapId, TargetSpawnId);

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 0;
        Monitorable = true;

        if (DrawPlaceholder)
            AddChild(new Sprite2D { Texture = BuildTexture() });

        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(16, 16) },
        });

        // Full-tile blocker: the door fills a wall gap, so it must stop movement
        // across the whole cell (Area2Ds don't collide with bodies).
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(16, 16) },
        });
        AddChild(blocker);
    }

    private static ImageTexture BuildTexture()
    {
        var wood = new Color("7a5a34");
        var panel = wood.Darkened(0.2f);
        var knob = new Color("d8b84a");

        var img = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        img.Fill(wood);
        img.FillRect(new Rect2I(3, 2, 4, 12), panel);  // left panel
        img.FillRect(new Rect2I(9, 2, 4, 12), panel);  // right panel
        img.FillRect(new Rect2I(12, 8, 2, 2), knob);
        return ImageTexture.CreateFromImage(img);
    }
}
