using Godot;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Pause overlay. Toggles the Paused phase from the pause action; its own
/// visibility is driven ONLY by GameState.StateChanged so every entry into/out of
/// Paused (from any caller) shows/hides it consistently.</summary>
public partial class PauseMenu : Control
{
    private Label _feedbackLabel = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        BuildControls();

        GameState.Instance.StateChanged += OnStateChanged;
    }

    public override void _ExitTree()
    {
        GameState.Instance.StateChanged -= OnStateChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("pause"))
            return;

        var phase = GameState.Instance.Current;
        if (phase == GameState.Phase.Playing)
            GameState.Instance.TransitionTo(GameState.Phase.Paused);
        else if (phase == GameState.Phase.Paused)
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        else
            return; // pause key is inert in Dialogue/Cutscene/Sleeping

        GetViewport().SetInputAsHandled();
    }

    private void BuildControls()
    {
        var dim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.5f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var panel = new Panel { CustomMinimumSize = new Vector2(220, 190) };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 16;
        vbox.OffsetTop = 12;
        vbox.OffsetRight = -16;
        vbox.OffsetBottom = -12;
        vbox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(vbox);

        vbox.AddChild(new Label { Text = "Paused", HorizontalAlignment = HorizontalAlignment.Center });

        // Button.Pressed is a Godot signal (auto-disconnects when the button is freed
        // with this menu), unlike the plain C# events unsubscribed in _ExitTree.
        var resume = new Button { Text = "Resume" };
        resume.Pressed += OnResumePressed;
        vbox.AddChild(resume);

        var save = new Button { Text = "Save" };
        save.Pressed += OnSavePressed;
        vbox.AddChild(save);

        var quit = new Button { Text = "Quit" };
        quit.Pressed += OnQuitPressed;
        vbox.AddChild(quit);

        _feedbackLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _feedbackLabel.AddThemeFontSizeOverride("font_size", 10);
        vbox.AddChild(_feedbackLabel);
    }

    private void OnStateChanged(GameState.Phase from, GameState.Phase to)
    {
        var paused = to == GameState.Phase.Paused;
        Visible = paused;
        MouseFilter = paused ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        if (paused)
            _feedbackLabel.Text = "";
    }

    private void OnResumePressed()
    {
        GameState.Instance.TransitionTo(GameState.Phase.Playing);
    }

    private void OnSavePressed()
    {
        _feedbackLabel.Text = SaveService.Instance.Save() ? "Saved." : "Save failed.";
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
