using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Transient top-center banners for quest hand-outs and completions,
/// derived from the story-flag stream: every NEW flag is asked which quests it just
/// started (QuestRules.StartedBy) and which it just completed (CompletedBy), and
/// each answer queues one banner — completion in gold, shown a few seconds, one at a
/// time. Passive overlay: no phase coupling, no input, sits above every panel and
/// under ScreenFade (a banner queued behind the sleep fade simply plays out unseen —
/// no shipped quest completes at dawn).</summary>
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
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.StoryFlagSet -= OnStoryFlagSet;
    }

    private void OnStoryFlagSet(string flagId, long dayStamped)
    {
        GameData data = SaveService.Instance.Current;
        foreach (QuestDef quest in QuestRules.StartedBy(flagId, data))
        {
            _pending.Enqueue(($"New quest: {quest.Title}", false));
        }
        foreach (QuestDef quest in QuestRules.CompletedBy(flagId, data))
        {
            _pending.Enqueue(($"Quest complete: {quest.Title}", true));
        }
        if (!_panel.Visible)
        {
            ShowNext();
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
