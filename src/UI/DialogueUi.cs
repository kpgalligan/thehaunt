using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Bottom dialogue box. Pure view over WorldSim's dialogue session: renders on
/// DialogueStarted/DialogueAdvanced, hides on DialogueFinished and SaveService.AfterLoad
/// (a load nulls the session without applying flags). Sits between StaminaBar and
/// PauseMenu in Main's UI layer so the pause overlay and screen fade draw above it.
/// Default process mode is deliberate: the tree is never paused during Dialogue (only the
/// Paused phase pauses), so this control must not fight the pause system.</summary>
public partial class DialogueUi : Control
{
    private PanelContainer _panel = null!;
    private Label _speaker = null!;
    private Label _body = null!;
    private Label _hint = null!;
    private VBoxContainer _choices = null!;

    // Engine frame the box became visible. The interact press that STARTED the dialogue
    // may still be dispatching this frame — it must not also consume line 1 (insurance
    // against input-order variance; _UnhandledInput runs before _PhysicsProcess).
    private ulong _openedFrame;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        BuildControls();

        WorldSim.Instance.DialogueStarted += OnDialogueStarted;
        WorldSim.Instance.DialogueAdvanced += OnDialogueAdvanced;
        WorldSim.Instance.DialogueFinished += OnDialogueFinished;
        SaveService.Instance.AfterLoad += OnAfterLoad;
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.DialogueStarted -= OnDialogueStarted;
        WorldSim.Instance.DialogueAdvanced -= OnDialogueAdvanced;
        WorldSim.Instance.DialogueFinished -= OnDialogueFinished;
        SaveService.Instance.AfterLoad -= OnAfterLoad;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (WorldSim.Instance.ActiveDialogue is not { Finished: false } session)
        {
            return;
        }
        if (Engine.GetProcessFrames() == _openedFrame)
        {
            return; // the opening interact press must not advance past line 1
        }
        if (session.AtChoices)
        {
            return; // the focused button consumes ui_accept via normal Control input
        }

        if (@event.IsActionPressed("interact") || @event.IsActionPressed("use_tool"))
        {
            WorldSim.Instance.AdvanceDialogue();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildControls()
    {
        _panel = new PanelContainer();
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _panel.OffsetLeft = 8;
        _panel.OffsetRight = -8;
        _panel.OffsetTop = -72; // ~64 px tall above the 8 px bottom margin
        _panel.OffsetBottom = -8;
        _panel.GrowHorizontal = GrowDirection.Both;
        _panel.GrowVertical = GrowDirection.Begin; // a choice list taller than 84 px expands upward
        AddChild(_panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        _panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        margin.AddChild(vbox);

        _speaker = new Label();
        _speaker.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f));
        vbox.AddChild(_speaker);

        _body = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        vbox.AddChild(_body);

        _choices = new VBoxContainer { Visible = false };
        _choices.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(_choices);

        _hint = new Label { Text = "▼", HorizontalAlignment = HorizontalAlignment.Right };
        _hint.AddThemeFontSizeOverride("font_size", 8);
        vbox.AddChild(_hint);
    }

    private void OnDialogueStarted(DialogueSession session)
    {
        _openedFrame = Engine.GetProcessFrames();
        Visible = true;
        Render(session);
    }

    private void OnDialogueAdvanced(DialogueSession session) => Render(session);

    private void OnDialogueFinished(string defId) => HideBox();

    private void OnAfterLoad() => HideBox();

    private void HideBox()
    {
        Visible = false;
        ClearChoiceButtons();
    }

    private void Render(DialogueSession session)
    {
        if (session.Finished)
        {
            return; // DialogueFinished owns the teardown
        }

        DialogueLine line = session.CurrentLine;
        bool narration = string.IsNullOrEmpty(line.SpeakerRole); // "" = narration
        _speaker.Visible = !narration;
        _speaker.Text = narration ? "" : NpcDefs.TryGet(line.SpeakerRole)?.DisplayRole ?? "";
        _body.Text = line.Text;

        ClearChoiceButtons();
        bool atChoices = session.AtChoices;
        _choices.Visible = atChoices;
        _hint.Visible = !atChoices;
        if (!atChoices)
        {
            return;
        }

        for (int i = 0; i < session.CurrentChoices.Count; i++)
        {
            int index = i; // captured per button
            var button = new Button { Text = session.CurrentChoices[i].Text };
            // Button.Pressed is a Godot signal (auto-disconnects when the button is
            // freed), unlike the plain C# events unsubscribed in _ExitTree.
            button.Pressed += () => WorldSim.Instance.ChooseDialogueOption(index);
            _choices.AddChild(button);
            if (i == 0)
            {
                button.GrabFocus(); // VBox order gives ui_up/ui_down neighbors automatically
            }
        }
    }

    private void ClearChoiceButtons()
    {
        // RemoveChild before QueueFree so a same-frame rebuild never shows dying and new
        // buttons together; QueueFree (never Free) because the pressed button's signal
        // emission can still be on the stack when a choice triggers this rebuild.
        foreach (Node child in _choices.GetChildren())
        {
            _choices.RemoveChild(child);
            child.QueueFree();
        }
    }
}
