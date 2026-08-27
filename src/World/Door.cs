using Godot;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Interactable doorway. Interacting requests travel through the bus; Main owns
/// the actual fade/swap flow. Sits flush in a wall gap — its own blocker seals
/// the gap so travel only ever happens via the interact press.
///
/// A door can be LOCKED behind a story flag (the motel's guest rooms). A locked door
/// must not fail silently: the prompt still appears, and interacting answers with a
/// line instead of doing nothing — rattling four handles in a row should feel
/// watched, and it only does if the game answers each time (motel handoff).
/// </summary>
public partial class Door : Area2D, IInteractable
{
    private const double LineSeconds = 3.0;

    [Export] public string TargetMapId { get; set; } = "";
    [Export] public string TargetSpawnId { get; set; } = "default";

    /// <summary>Story flag that unlocks this door; empty = never locked. Checked live
    /// at every interact, so an unlock needs no repaint.</summary>
    [Export] public string RequiredFlag { get; set; } = "";

    /// <summary>The answer a locked handle gives.</summary>
    [Export] public string LockedMessage { get; set; } = "Locked.";

    /// <summary>
    /// False where the doorway is already drawn into the map's art (the town facades):
    /// the node still carries the blocker and the prompt, it just draws nothing.
    /// </summary>
    [Export] public bool DrawPlaceholder { get; set; } = true;

    public string PromptText => "Enter";

    /// <summary>Locked while its flag is unset. A door with no target map at all (a
    /// handle that will never open) is permanently locked.</summary>
    public bool IsLocked =>
        TargetMapId.Length == 0
        || (RequiredFlag.Length > 0 && !SaveService.Instance.Current.HasFlag(RequiredFlag));

    // The IsQueuedForDeletion guard closes the one-frame freed-but-overlapped
    // probe window during a map swap.
    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.PlayerHasControl && !IsQueuedForDeletion();

    public void Interact(Node2D interactor)
    {
        if (IsLocked)
        {
            ShowLine(LockedMessage);
            return;
        }
        WorldSim.Instance.RequestTravel(TargetMapId, TargetSpawnId);
    }

    private Label? _line;
    private int _lineToken; // invalidates stale hide timers, like Sign's

    private void ShowLine(string text)
    {
        _line ??= BuildLine();
        _line.Text = text;
        _line.ResetSize();
        Vector2 scaled = _line.Size * _line.Scale;
        _line.Position = new Vector2(-scaled.X / 2f, -20f - scaled.Y / 2f);
        _line.Show();

        int token = ++_lineToken;
        GetTree().CreateTimer(LineSeconds, processAlways: false).Timeout += () =>
        {
            if (IsInstanceValid(_line) && token == _lineToken)
                _line.Hide();
        };
    }

    private Label BuildLine()
    {
        var label = new Label
        {
            Visible = false,
            Scale = new Vector2(0.5f, 0.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(label);
        return label;
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
