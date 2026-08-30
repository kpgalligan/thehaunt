using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Transient top-center banners — the game's one general message queue.
/// Producers: the story-flag stream (every NEW flag is asked which quests it just
/// started via QuestRules.StartedBy / completed via CompletedBy), the garage
/// (a customer's drop-off is the on-screen "message from Mike"; a finished repair
/// banners its payday), and skill level-ups. Completions and level-ups in gold,
/// a few seconds each, one at a time. Passive overlay: no phase coupling, no
/// input, sits above every panel and under ScreenFade (a banner queued behind the
/// sleep fade simply plays out unseen — which is why dawn garage payments go to
/// the overnight REPORT, never here).</summary>
public partial class QuestToastUi : Control
{
    private const double ShowSeconds = 3.5;

    private PanelContainer _panel = null!;
    private Label _label = null!;
    private Godot.Timer _timer = null!;
    private readonly Queue<(string Text, bool Gold)> _pending = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _panel = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.08f, 0.9f),
            BorderColor = new Color(1f, 0.92f, 0.55f, 0.6f),
        };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(6);
        _panel.AddThemeStyleboxOverride("panel", style);
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.Minsize, margin: 12);
        _panel.GrowHorizontal = GrowDirection.Both;
        _panel.GrowVertical = GrowDirection.End;
        AddChild(_panel);

        _label = new Label();
        _label.AddThemeFontSizeOverride("font_size", 8);
        _panel.AddChild(_label);

        _timer = new Godot.Timer { WaitTime = ShowSeconds, OneShot = true };
        _timer.Timeout += OnTimerOut;
        AddChild(_timer);

        WorldSim.Instance.StoryFlagSet += OnStoryFlagSet;
        WorldSim.Instance.GarageCustomerArrived += OnGarageCustomerArrived;
        WorldSim.Instance.GarageJobCompleted += OnGarageJobCompleted;
        WorldSim.Instance.SkillLeveledUp += OnSkillLeveledUp;
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.StoryFlagSet -= OnStoryFlagSet;
        WorldSim.Instance.GarageCustomerArrived -= OnGarageCustomerArrived;
        WorldSim.Instance.GarageJobCompleted -= OnGarageJobCompleted;
        WorldSim.Instance.SkillLeveledUp -= OnSkillLeveledUp;
    }

    // The on-screen half of Kevin's "Jane will get a message from Mike" (the
    // quest-task half lives in the quest log's garage section). "Word from" on
    // purpose — how word travels is unexplained, and the one modern object in
    // town stays the scooter.
    private void OnGarageCustomerArrived(GarageJobRecord job)
    {
        string service = GarageServices.TryGet(job.ServiceId)?.Name ?? "Repair";
        Enqueue($"Word from Mike: a customer dropped off a car. {service}.", gold: false);   // [KEVIN]
    }

    private void OnGarageJobCompleted(GarageJobRecord job)
    {
        string service = GarageServices.TryGet(job.ServiceId)?.Name ?? "Repair";
        Enqueue($"{service} — done. Payment tomorrow.", gold: true);   // [KEVIN]
    }

    private void OnSkillLeveledUp(string skillId, int newLevel) =>
        Enqueue($"{SkillIds.DisplayName(skillId)} — level {newLevel}", gold: true);

    private void Enqueue(string text, bool gold)
    {
        _pending.Enqueue((text, gold));
        if (!_panel.Visible)
        {
            ShowNext();
        }
    }

    private void OnStoryFlagSet(string flagId, long dayStamped)
    {
        GameData data = SaveService.Instance.Current;
        foreach (QuestDef quest in QuestRules.StartedBy(flagId, data))
        {
            Enqueue($"New quest: {quest.Title}", gold: false);
        }
        foreach (QuestDef quest in QuestRules.CompletedBy(flagId, data))
        {
            Enqueue($"Quest complete: {quest.Title}", gold: true);
        }
    }

    private void OnTimerOut()
    {
        _panel.Visible = false;
        ShowNext();
    }

    private void ShowNext()
    {
        if (_pending.Count == 0)
        {
            return;
        }
        (string text, bool gold) = _pending.Dequeue();
        _label.Text = text;
        _label.AddThemeColorOverride("font_color",
            gold ? new Color(1f, 0.92f, 0.55f) : new Color(1f, 1f, 1f, 0.9f));
        _panel.ResetSize();
        _panel.Visible = true;
        _timer.Start();
    }
}
