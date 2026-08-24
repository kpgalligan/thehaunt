using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Top-right date/time readout. Display-only: listens to MinuteTicked per the
/// standing rule that gameplay systems use TenMinuteTicked instead.</summary>
public partial class Hud : Control
{
    private Label _dateLabel = null!;
    private Label _timeLabel = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, margin: 8);
        panel.GrowHorizontal = GrowDirection.Begin;
        AddChild(panel);

        var vbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(vbox);

        _dateLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        vbox.AddChild(_dateLabel);

        _timeLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        vbox.AddChild(_timeLabel);

        Clock.Instance.MinuteTicked += OnTimeChanged;
        Clock.Instance.DayStarted += OnTimeChanged;
        SaveService.Instance.AfterLoad += OnAfterLoad;

        UpdateLabels();
    }

    public override void _ExitTree()
    {
        Clock.Instance.MinuteTicked -= OnTimeChanged;
        Clock.Instance.DayStarted -= OnTimeChanged;
        SaveService.Instance.AfterLoad -= OnAfterLoad;
    }

    private void OnTimeChanged(GameTime time) => UpdateLabels();

    private void OnAfterLoad() => UpdateLabels();

    private void UpdateLabels()
    {
        var now = Clock.Instance.Now;
        _dateLabel.Text = now.ToDateString();
        _timeLabel.Text = now.ToClockString();
    }
}
