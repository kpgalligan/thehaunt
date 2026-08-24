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

        // Solid blocker so the sign stops movement (Area2Ds don't collide with bodies).
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(12, 10) },
        });
        AddChild(blocker);

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
