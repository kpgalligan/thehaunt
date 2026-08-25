using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Morning shipment card. Latches WorldSim.OvernightCompleted — which fires
/// synchronously mid-AdvanceToDayStart with the screen black, so it must never display
/// during the event — and shows only when Main's sleep flow awaits ShowIfPendingAsync
/// after the fade-in. The phase is still Sleeping while the card is up (player frozen,
/// PauseMenu inert, StoryDirector bails), so a dismiss press is the only way forward;
/// money is credited + autosaved before the card, so quitting mid-card loses only the
/// popup. Zero-proceeds mornings show nothing (CropsGrown alone never interrupts).</summary>
public partial class OvernightReportUi : Control
{
    private VBoxContainer _lines = null!;
    private Label _totalLabel = null!;

    private OvernightReport _latched;
    private bool _hasLatched;
    private TaskCompletionSource? _waiter;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        BuildControls();

        WorldSim.Instance.OvernightCompleted += OnOvernightCompleted;
        SaveService.Instance.AfterLoad += OnAfterLoad;
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.OvernightCompleted -= OnOvernightCompleted;
        SaveService.Instance.AfterLoad -= OnAfterLoad;
        // Freed-Main safety: a hanging awaiter would strand the sleep flow's finally.
        _hasLatched = false;
        Dismiss();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_waiter is null)
        {
            return;
        }
        if (@event.IsActionPressed("interact")
            || @event.IsActionPressed("ui_accept")
            || @event.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            Dismiss();
        }
    }

    /// <summary>Awaited by Main.RunSleepFlow after the fade-in. Returns a completed task
    /// when there is nothing to show; otherwise shows the card and completes on the
    /// dismissing press (or a force-complete via load / tree exit).</summary>
    public Task ShowIfPendingAsync()
    {
        OvernightReport report = _latched;
        bool pending = _hasLatched;
        _hasLatched = false;
        if (!pending || report.ShippingProceeds <= 0 || report.Sales is not { Count: > 0 } sales)
        {
            return Task.CompletedTask; // zero-proceeds mornings show NOTHING
        }

        Render(sales, report.ShippingProceeds);
        Visible = true;
        _waiter = new TaskCompletionSource();
        return _waiter.Task;
    }

    /// <summary>Hides the card and completes the pending wait (also used by tests).</summary>
    public void Dismiss()
    {
        Visible = false;
        ClearLines();
        TaskCompletionSource? waiter = _waiter;
        _waiter = null;
        waiter?.TrySetResult();
    }

    // Latch only — the screen is black and the world is mid-mutation when this fires.
    private void OnOvernightCompleted(OvernightReport report)
    {
        _latched = report;
        _hasLatched = true;
    }

    // A load discards the reported world: drop the latch and force-complete the wait
    // so the (now stale) sleep flow's finally can run.
    private void OnAfterLoad()
    {
        _hasLatched = false;
        Dismiss();
    }

    private void BuildControls()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(180, 0) };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        margin.AddChild(vbox);

        var title = new Label
        {
            Text = "Overnight Shipment", // [KEVIN] report copy
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f));
        vbox.AddChild(title);

        _lines = new VBoxContainer();
        _lines.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(_lines);

        vbox.AddChild(new HSeparator());

        _totalLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        vbox.AddChild(_totalLabel);

        var hint = new Label { Text = "▼", HorizontalAlignment = HorizontalAlignment.Right };
        hint.AddThemeFontSizeOverride("font_size", 10);
        vbox.AddChild(hint);
    }

    private void Render(IReadOnlyList<ShippedLine> sales, long total)
    {
        ClearLines();
        foreach (ShippedLine line in sales)
        {
            // Display name via ItemDefs; '?' for unknown ids (they cannot actually
            // appear here — unsellable stacks stay binned — but the rule is uniform).
            string name = ItemDefs.TryGet(line.ItemId)?.Name ?? "?";
            _lines.AddChild(new Label { Text = $"{name} x{line.Count} — {line.Proceeds}g" }); // [KEVIN] line copy
        }
        _totalLabel.Text = $"+{total}g";
    }

    private void ClearLines()
    {
        // RemoveChild before QueueFree so a same-frame redisplay never shows dying and
        // new lines together (DialogueUi's choice-rebuild pattern).
        foreach (Node child in _lines.GetChildren())
        {
            _lines.RemoveChild(child);
            child.QueueFree();
        }
    }
}
