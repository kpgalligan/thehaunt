using Godot;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Sell crate. Interacting deposits the whole selected stack into the shipping
/// bin model; OvernightSim turns the bin's contents into money while the player
/// sleeps.
/// </summary>
public partial class ShippingBin : Area2D, IInteractable
{
    public string PromptText => "Ship";

    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.Current == GameState.Phase.Playing;

    public void Interact(Node2D interactor) =>
        WorldSim.Instance.DepositSelectedToBin();

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 0;
        Monitorable = true;

        AddChild(new Sprite2D { Texture = BuildTexture() });

        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(16, 16) },
        });

        // Solid blocker so the bin stops movement (Area2Ds don't collide with bodies).
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(12, 12) },
        });
        AddChild(blocker);
    }

    private static ImageTexture BuildTexture()
    {
        var frame = new Color("9a7a4a");
        var slat = frame.Darkened(0.3f);

        var img = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        img.Fill(frame);
        img.FillRect(new Rect2I(2, 2, 12, 12), slat);       // dark interior slats
        img.FillRect(new Rect2I(2, 6, 12, 1), frame);       // horizontal plank lines
        img.FillRect(new Rect2I(2, 10, 12, 1), frame);
        return ImageTexture.CreateFromImage(img);
    }
}
