using Godot;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Sleeping spot. Interacting hands control to Main via the Sleeping phase;
/// this node owns none of the sleep flow itself.
/// </summary>
public partial class Bed : Area2D, IInteractable
{
    public string PromptText => "Sleep";

    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.Current == GameState.Phase.Playing;

    public void Interact(Node2D interactor) =>
        GameState.Instance.TransitionTo(GameState.Phase.Sleeping);

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 0;
        Monitorable = true;

        AddChild(new Sprite2D { Texture = BuildTexture() });

        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(16, 32) },
        });

        // Solid blocker so the bed stops movement (Area2Ds don't collide with bodies).
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(14, 30) },
        });
        AddChild(blocker);
    }

    private static ImageTexture BuildTexture()
    {
        var frame = new Color("8a5a3a");
        var blanket = new Color("b03a3a");
        var pillow = new Color("e8e4f0");

        var img = Image.CreateEmpty(16, 32, false, Image.Format.Rgba8);
        img.Fill(frame);
        img.FillRect(new Rect2I(2, 3, 12, 7), pillow);    // pillow at the head
        img.FillRect(new Rect2I(2, 12, 12, 18), blanket); // blanket over the rest
        return ImageTexture.CreateFromImage(img);
    }
}
