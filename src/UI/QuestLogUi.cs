using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Quest log, toggled with J ("toggle_quests"). Non-modal exactly like
/// HelpPanel: no phase change, the clock keeps running, and losing player control
/// force-hides it. Content is a pure derivation (QuestRules over the story flags):
/// active quests with their descriptions, then completed ones dimmed — rebuilt on
/// every show and on every new story flag while visible, so a quest completing under
/// an open log updates in place.</summary>
public partial class QuestLogUi : Control
{
    private VBoxContainer _rows = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        BuildControls();

        GameState.Instance.StateChanged += OnStateChanged;
        WorldSim.Instance.StoryFlagSet += OnStoryFlagSet;
    }

    public override void _ExitTree()
    {
        GameState.Instance.StateChanged -= OnStateChanged;
        WorldSim.Instance.StoryFlagSet -= OnStoryFlagSet;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!GameState.Instance.PlayerHasControl)
        {
            return; // leave the press unhandled — the log only toggles in free play
        }
        if (@event.IsActionPressed("toggle_quests"))
        {
            Visible = !Visible;
            if (Visible)
            {
                Rebuild();
            }
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
        // Right-center: clear of the HelpPanel on the left, the HUD corner and the
        // hotbar — the two non-modal panels can stand open together.
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight, LayoutPresetMode.Minsize, margin: 8);
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.GrowVertical = GrowDirection.Both;
        AddChild(panel);

        _rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _rows.AddThemeConstantOverride("separation", 2);
        panel.AddChild(_rows);
    }

    private void Rebuild()
    {
        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        var title = new Label { Text = "Quests" };
        title.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f));
        _rows.AddChild(title);

        GameData data = SaveService.Instance.Current;
        var active = QuestRules.ActiveQuests(data);
        var completed = QuestRules.CompletedQuests(data);

        foreach (QuestDef quest in active)
        {
            var name = new Label { Text = quest.Title };
            name.AddThemeFontSizeOverride("font_size", 8);
            _rows.AddChild(name);
            var desc = new Label
            {
                Text = quest.Description,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(150, 0),
            };
            desc.AddThemeFontSizeOverride("font_size", 8);
            desc.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.6f));
            _rows.AddChild(desc);
        }
        foreach (QuestDef quest in completed)
        {
            var done = new Label { Text = "✓ " + quest.Title };
            done.AddThemeFontSizeOverride("font_size", 8);
            done.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.4f));
            _rows.AddChild(done);
        }
        if (active.Count == 0 && completed.Count == 0)
        {
            var empty = new Label { Text = "Nothing yet." };
            empty.AddThemeFontSizeOverride("font_size", 8);
            empty.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.5f));
            _rows.AddChild(empty);
        }
    }

    private void OnStoryFlagSet(string flagId, long dayStamped)
    {
        if (Visible)
        {
            Rebuild();   // a quest starting or completing under an open log updates live
        }
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
