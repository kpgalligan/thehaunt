using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Quest log, toggled with J ("toggle_quests"). Non-modal exactly like
/// HelpPanel: no phase change, the clock keeps running, and losing player control
/// force-hides it. Content: active quests with their descriptions (QuestRules over
/// the story flags), then the garage's live jobs — Kevin's "recorded as quest
/// tasks": one row per car with its deadline (progress lives on the shop floor's
/// bay labels, not here), straight off
/// GameData.GarageJobs — then completed quests dimmed. Rebuilt on every show, on
/// every new story flag, and on every GarageJobsChanged while visible, so a quest
/// completing or a customer arriving under an open log updates in place.</summary>
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
        WorldSim.Instance.GarageJobsChanged += OnGarageJobsChanged;
    }

    public override void _ExitTree()
    {
        GameState.Instance.StateChanged -= OnStateChanged;
        WorldSim.Instance.StoryFlagSet -= OnStoryFlagSet;
        WorldSim.Instance.GarageJobsChanged -= OnGarageJobsChanged;
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
        // The garage's live tasks (Kevin: arrivals are "recorded as quest tasks").
        // Dynamic model state, not QuestDefs — jobs come and go, quests are
        // monotone flag windows; the two never share machinery.
        if (data.GarageJobs.Count > 0)
        {
            var header = new Label { Text = "Garage" };
            header.AddThemeFontSizeOverride("font_size", 8);
            header.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f, 0.9f));
            _rows.AddChild(header);
            long today = Clock.Instance.Now.DayIndex;
            foreach (GarageJobRecord job in data.GarageJobs)
            {
                var row = new Label { Text = GarageRow(job, today) };
                row.AddThemeFontSizeOverride("font_size", 8);
                row.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, job.Completed ? 0.4f : 0.8f));
                _rows.AddChild(row);
            }
        }
        foreach (QuestDef quest in completed)
        {
            var done = new Label { Text = "✓ " + quest.Title };
            done.AddThemeFontSizeOverride("font_size", 8);
            done.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.4f));
            _rows.AddChild(done);
        }
        if (active.Count == 0 && completed.Count == 0 && data.GarageJobs.Count == 0)
        {
            var empty = new Label { Text = "Nothing yet." };
            empty.AddThemeFontSizeOverride("font_size", 8);
            empty.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.5f));
            _rows.AddChild(empty);
        }
    }

    // Deadline surface for the 2-day window: the customer reclaims an unfinished
    // car at dawn of ArrivalDay + 2, so day D reads "due tomorrow", D+1 "due
    // today". "Overdue" only on hand-edited clocks — dawn removes real ones.
    private static string GarageRow(GarageJobRecord job, long today)
    {
        string service = GarageServices.TryGet(job.ServiceId)?.Name ?? "Repair";
        if (job.Completed)
        {
            return $"✓ {service} — payment tomorrow";   // [KEVIN]
        }
        long lastDay = job.ArrivalDay + 1;
        string due = today < lastDay ? "due tomorrow"
            : today == lastDay ? "due today"
            : "overdue";   // [KEVIN]
        return $"{service} — {due}";
    }

    private void OnGarageJobsChanged()
    {
        if (Visible)
        {
            Rebuild();   // an arrival or a work press under an open log updates live
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
