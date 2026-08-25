using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Top-right date/time/money readout. Display-only: listens to MinuteTicked per
/// the standing rule that gameplay systems use TenMinuteTicked instead.</summary>
public partial class Hud : Control
{
    private Label _dateLabel = null!;
    private Label _timeLabel = null!;
    private Label _moneyLabel = null!;

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

        _moneyLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        vbox.AddChild(_moneyLabel);

        Clock.Instance.MinuteTicked += OnTimeChanged;
        Clock.Instance.DayStarted += OnTimeChanged;
        SaveService.Instance.AfterLoad += OnAfterLoad;
        WorldSim.Instance.MoneyChanged += OnMoneyChanged;

        UpdateLabels();
        UpdateMoney();
    }

    public override void _ExitTree()
    {
        Clock.Instance.MinuteTicked -= OnTimeChanged;
        Clock.Instance.DayStarted -= OnTimeChanged;
        SaveService.Instance.AfterLoad -= OnAfterLoad;
        WorldSim.Instance.MoneyChanged -= OnMoneyChanged;
    }

    private void OnTimeChanged(GameTime time) => UpdateLabels();

    private void OnAfterLoad()
    {
        UpdateLabels();
        UpdateMoney();
    }

    private void OnMoneyChanged(long money) => _moneyLabel.Text = $"{money}g";

    private void UpdateLabels()
    {
        var now = Clock.Instance.Now;
        _dateLabel.Text = now.ToDateString();
        _timeLabel.Text = now.ToClockString();
    }

    private void UpdateMoney()
    {
        _moneyLabel.Text = $"{SaveService.Instance.Current.Player.Money}g";
    }
}
