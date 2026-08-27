using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Sell crate. Interacting deposits the whole selected stack into the shipping bin
/// model; OvernightSim turns the bin's contents into money while the player sleeps.
///
/// The drawn bin is two tiles wide and has a lid: it stands open with produce showing
/// while the model's bin holds anything, and shut once the night has sold it. A pure
/// view of <see cref="GameData.ShippingBin"/> — the node stores nothing.
/// </summary>
public partial class ShippingBin : Area2D, IInteractable
{
    /// <summary>Closed/open atlas regions; zero-size falls back to the placeholder.</summary>
    public Rect2 ClosedSource { get; init; }

    public Rect2 OpenSource { get; init; }

    public string ArtPath { get; init; } = FarmBuildings.TexturePath;

    private Sprite2D? _sprite;

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

        bool drawn = ClosedSource.Size != Vector2.Zero;
        Vector2 size = drawn ? ClosedSource.Size : new Vector2(16, 16);

        _sprite = drawn
            ? Prop.Cut(ArtPath, ClosedSource)
            : new Sprite2D { Texture = BuildTexture() };
        AddChild(_sprite);

        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });

        // Solid blocker so the bin stops movement (Area2Ds don't collide with bodies).
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = size - new Vector2(4, 4) },
        });
        AddChild(blocker);

        if (drawn)
        {
            // One subscription is enough for both events that can change the bin: a
            // deposit fires InventoryChanged, and so does WorldSim's dawn ordering
            // (step 3) after the overnight sale has already emptied it on DayEnded. An
            // entity never subscribes to the clock itself — systems do.
            WorldSim.Instance.InventoryChanged += ApplyLid;
            ApplyLid();
        }
    }

    public override void _ExitTree() => WorldSim.Instance.InventoryChanged -= ApplyLid;

    private void ApplyLid()
    {
        if (_sprite == null || OpenSource.Size == Vector2.Zero)
            return;
        bool holding = SaveService.Instance.Current.ShippingBin.Count > 0;
        _sprite.RegionRect = holding ? OpenSource : ClosedSource;
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
