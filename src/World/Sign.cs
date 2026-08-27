using Godot;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Readable sign. Displays its message in a self-owned floating label for a few
/// seconds — no UI/dialogue coupling in the foundation.
/// </summary>
public partial class Sign : Area2D, IInteractable
{
    private const double ShowSeconds = 3.0;

    [Export] public string Message { get; set; } = "";

    /// <summary>
    /// False where the sign is already drawn by real art (the motel's pole sign, the
    /// drive-in's marquee): the node still carries its read area and its blocker, it
    /// just draws nothing — the same contract as <see cref="Door.DrawPlaceholder"/>.
    /// </summary>
    [Export] public bool DrawPlaceholder { get; set; } = true;

    public string PromptText => "Read";

    private Label _label = null!;
    private int _showToken; // invalidates stale hide timers when re-read while visible

    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.Current == GameState.Phase.Playing;

    public void Interact(Node2D interactor)
    {
        _label.Text = Message;
        _label.ResetSize();
        Vector2 scaled = _label.Size * _label.Scale;
        _label.Position = new Vector2(-scaled.X / 2f, -20f - scaled.Y / 2f);
        _label.Show();

        int token = ++_showToken;
        // processAlways: false — the display window freezes with the world under tree pause.
        GetTree().CreateTimer(ShowSeconds, processAlways: false).Timeout += () =>
        {
            if (IsInstanceValid(_label) && token == _showToken)
                _label.Hide();
        };
    }

    private StaticBody2D? _blocker;

    /// <summary>
    /// Whether the sign is standing here at all — for a sign that only exists while some
    /// story flag is unset. Visibility is NOT enough on its own: a StaticBody2D under a
    /// hidden parent still collides, so hiding a sign without this leaves an invisible
    /// wall on its tile.
    /// </summary>
    public void SetPresent(bool present)
    {
        Visible = present;
        SetDeferred(Area2D.PropertyName.Monitorable, present);
        _blocker?.SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, present ? 1 : 0);
    }

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

        // Solid blocker so the sign stops movement (Area2Ds don't collide with bodies).
        _blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        _blocker.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(12, 10) },
        });
        AddChild(_blocker);

        _label = new Label
        {
            Visible = false,
            Scale = new Vector2(0.5f, 0.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_label);
    }

    private static ImageTexture BuildTexture()
    {
        var wood = new Color("9a7a4a");

        var img = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        img.FillRect(new Rect2I(1, 2, 14, 8), wood); // board
        img.FillRect(new Rect2I(7, 10, 2, 6), wood); // post
        return ImageTexture.CreateFromImage(img);
    }
}
