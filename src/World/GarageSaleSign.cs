using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The FOR SALE board in front of the west entry's closed repair garage. Before
/// the deed a press opens WorldSim's garage-sale session — the confirm panel owns
/// the price talk, so an E at the board can never spend anything by itself. After
/// the deed the same node answers with a line, checked live at every interact like
/// <see cref="Door.RequiredFlag"/>, so the purchase needs no repaint here. The
/// board is drawn pale against <see cref="Sign"/>'s wood: the realtor's plank, not
/// the town's signage (which keeps to its four mounts).
/// </summary>
public partial class GarageSaleSign : Area2D, IInteractable
{
    private const double ShowSeconds = 3.0;

    public string PromptText =>
        GarageRules.IsOwned(SaveService.Instance.Current) ? "Read" : "For sale";

    private Label _label = null!;
    private int _showToken; // invalidates stale hide timers when re-read while visible

    public bool CanInteract(Node2D interactor) => GameState.Instance.PlayerHasControl;

    public void Interact(Node2D interactor)
    {
        if (!GarageRules.IsOwned(SaveService.Instance.Current))
        {
            WorldSim.Instance.OpenGarageSale();
            return;
        }
        // [KEVIN] placeholder copy — whether the board comes down after the sale
        // is staging for later; for now it simply answers.
        ShowLine("SOLD.");
    }

    private void ShowLine(string text)
    {
        _label.Text = text;
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

        // Solid blocker so the board stops movement (Area2Ds don't collide with bodies).
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
        // A pale realtor's plank with a rust-red stripe, on Sign's wood post — read
        // as "not one of ours" next to the town's lettered mounts.
        var face = new Color("ede3cb");
        var stripe = new Color("a4432f");
        var post = new Color("9a7a4a");

        var img = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        img.FillRect(new Rect2I(1, 2, 14, 8), face);   // board
        img.FillRect(new Rect2I(2, 4, 12, 2), stripe); // the notice line
        img.FillRect(new Rect2I(7, 10, 2, 6), post);   // post
        return ImageTexture.CreateFromImage(img);
    }
}
