using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Storage chest. Interacting opens the storage session on the bus
/// (WorldSim.OpenStorage); ChestUi drives the transfer UI off the
/// StorageOpened/StorageClosed events. The node holds NO items itself — the
/// stacks live in GameData.Storages under <see cref="StorageId"/>.
/// </summary>
public partial class Chest : Area2D, IInteractable
{
    [Export] public string StorageId { get; set; } = StorageIds.FarmHouseChest;

    public string PromptText => "Open";

    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.PlayerHasControl;

    public void Interact(Node2D interactor) =>
        WorldSim.Instance.OpenStorage(StorageId);

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

        // Solid blocker so the chest stops movement (Area2Ds don't collide with bodies).
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(12, 12) },
        });
        AddChild(blocker);
    }

    private static ImageTexture BuildTexture()
    {
        var wood = new Color("8a5a34");
        var lid = wood.Lightened(0.15f);
        var seam = wood.Darkened(0.35f);
        var latch = new Color("d8b84a");

        var img = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        img.Fill(wood);
        img.FillRect(new Rect2I(1, 2, 14, 5), lid);   // lid
        img.FillRect(new Rect2I(1, 7, 14, 1), seam);  // lid seam
        img.FillRect(new Rect2I(7, 6, 2, 3), latch);  // brass latch
        return ImageTexture.CreateFromImage(img);
    }
}
