using Godot;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Tab-toggled controls overlay. Pure non-modal: no phase change, the clock
/// keeps running and the player keeps control; the root ignores mouse ALWAYS and nothing
/// here takes focus. Force-hides whenever the new phase lacks control, so it can never
/// underlap the modal UIs (dialogue, chest, shop, mailbox, pause). Godot's built-in ui_focus_next
/// keeps Tab deliberately: focused Controls consume Tab in the GUI stage before
/// _UnhandledInput, and those contexts all lack PlayerHasControl anyway.</summary>
public partial class HelpPanel : Control
{
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
        if (!GameState.Instance.PlayerHasControl)
        {
            return; // leave the press unhandled — help only toggles in free play
        }
        if (@event.IsActionPressed("toggle_help"))
        {
            Visible = !Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildControls()
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.08f, 0.8f),
            BorderColor = new Color(1f, 1f, 1f, 0.25f),
        };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(8);
        panel.AddThemeStyleboxOverride("panel", style);
        // Left-center keeps clear of the top-right HUD and the bottom hotbar.
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft, LayoutPresetMode.Minsize, margin: 8);
        panel.GrowHorizontal = GrowDirection.End;
        panel.GrowVertical = GrowDirection.Both;
        AddChild(panel);

        var vbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        vbox.AddThemeConstantOverride("separation", 2);
        panel.AddChild(vbox);

        var title = new Label { Text = "Controls" };
        title.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f));
        vbox.AddChild(title);

        // Factual bindings only — no lore.
        vbox.AddChild(MakeRow("Move — WASD / Arrows"));
        vbox.AddChild(MakeRow("Interact — E / Space"));
        vbox.AddChild(MakeRow("Scooter — E to ride, E again to park"));
        vbox.AddChild(MakeRow("Use Tool — Left Click / C"));
        vbox.AddChild(MakeRow("Hotbar — 1-0 / Mouse Wheel"));
        vbox.AddChild(MakeRow("Chest/Shop — Arrows select, E move/buy, Esc close"));
        vbox.AddChild(MakeRow("Quests — J"));
        vbox.AddChild(MakeRow("Pause — Esc"));
        vbox.AddChild(MakeRow("Controls — Tab"));
    }

    private static Label MakeRow(string text)
    {
        var label = new Label { Text = text }; // Labels ignore mouse by default
        label.AddThemeFontSizeOverride("font_size", 8);
        return label;
    }

    private void OnStateChanged(GameState.Phase from, GameState.Phase to)
    {
        // Standing rule: gate on the derived queries, never on Phase compares.
        if (!GameState.Instance.PlayerHasControl)
        {
            Visible = false;
        }
    }
}
