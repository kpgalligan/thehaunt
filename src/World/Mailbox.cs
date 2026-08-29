using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The farm's mailbox. Interacting opens WorldSim's mailbox session (the Menu-phase
/// mail UI); this node owns none of the mail model — what it shows is a pure
/// derivation (<see cref="MailRules.HasUnread"/>), redrawn by <see cref="Refresh"/>,
/// which the map's ApplyState calls on every repaint (boot, dawn, every new story
/// flag — a read stamp lowers the signal through exactly that path).
/// Placeholder art until real art lands: a post-mounted box whose side flag stands
/// up, slot glowing, while unread mail waits.
/// </summary>
public partial class Mailbox : Area2D, IInteractable
{
    public string PromptText => "Mail";

    /// <summary>The signal as last drawn — tests pin the repaint wiring through this.</summary>
    public bool HasUnread => _hasUnread;

    private Sprite2D _sprite = null!;
    private ImageTexture? _idleTexture;
    private ImageTexture? _flaggedTexture;
    private bool _hasUnread;

    public bool CanInteract(Node2D interactor) => GameState.Instance.PlayerHasControl;

    public void Interact(Node2D interactor) => WorldSim.Instance.OpenMailbox();

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 0;
        Monitorable = true;

        // 16x32 drawn on the cell with the box overhanging the cell above —
        // the furniture convention (src/CLAUDE.md).
        _sprite = new Sprite2D { Position = new Vector2(0, -8) };
        AddChild(_sprite);

        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(16, 16) },
        });

        // Solid blocker so the mailbox stops movement (Area2Ds don't collide with bodies).
        var blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(12, 12) },
        });
        AddChild(blocker);

        Refresh();
        _sprite.Texture = _hasUnread ? _flaggedTexture : _idleTexture;
    }

    /// <summary>Re-derives the unread signal and swaps the drawn state to match.</summary>
    public void Refresh()
    {
        _hasUnread = MailRules.HasUnread(SaveService.Instance.Current, Clock.Instance.Now);
        if (_sprite != null)
        {
            _idleTexture ??= BuildTexture(flagUp: false);
            _flaggedTexture ??= BuildTexture(flagUp: true);
            _sprite.Texture = _hasUnread ? _flaggedTexture : _idleTexture;
        }
    }

    private static ImageTexture BuildTexture(bool flagUp)
    {
        var post = new Color("6b4a2f");
        var box = new Color("8d8f8a");
        var boxShade = new Color("6f716c");
        var slot = new Color("2b241d");
        var flag = new Color("b03a3a");

        var img = Image.CreateEmpty(16, 32, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        img.FillRect(new Rect2I(7, 14, 2, 14), post);       // post down to the ground
        img.FillRect(new Rect2I(3, 6, 10, 8), box);         // the box
        img.FillRect(new Rect2I(3, 12, 10, 2), boxShade);   // underside
        img.FillRect(new Rect2I(4, 8, 6, 2), slot);         // slot on the door
        if (flagUp)
        {
            img.FillRect(new Rect2I(13, 2, 1, 8), flag);    // arm raised
            img.FillRect(new Rect2I(13, 2, 3, 3), flag);    // pennant
        }
        else
        {
            img.FillRect(new Rect2I(13, 10, 3, 1), flag);   // arm folded flat
        }
        return ImageTexture.CreateFromImage(img);
    }
}
