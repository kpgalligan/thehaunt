using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Centered garage-sale panel — the confirm a 100,000g purchase deserves.
/// Pure view over WorldSim's garage-sale session: shows on GarageSaleOpened, hides on
/// GarageSaleClosed (a successful buy closes the session itself, and WorldSim fires a
/// Closed when a load lands mid-session). Two focus Buttons; WALK AWAY holds opening
/// focus, so a mashed E can never spend six figures — buying takes a deliberate arrow
/// first. Refusals flash beneath the buttons; Esc (the pause action) closes.</summary>
public partial class GarageSaleUi : Control
{
    private Label _moneyLabel = null!;
    private Label _flashLabel = null!;
    private Button _buyButton = null!;
    private Button _leaveButton = null!;
    private Godot.Timer _flashTimer = null!;

    // Engine frame the panel became visible. The interact press that OPENED the sale
    // may still be dispatching this frame — it must not also press a button
    // (insurance against input-order variance; DialogueUi's pattern).
    private ulong _openedFrame;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        BuildControls();

        WorldSim.Instance.GarageSaleOpened += OnOpened;
        WorldSim.Instance.GarageSaleClosed += OnClosed;
        WorldSim.Instance.MoneyChanged += OnMoneyChanged;
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.GarageSaleOpened -= OnOpened;
        WorldSim.Instance.GarageSaleClosed -= OnClosed;
        WorldSim.Instance.MoneyChanged -= OnMoneyChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!WorldSim.Instance.GarageSaleOpen)
        {
            return;
        }
        if (Engine.GetProcessFrames() == _openedFrame)
        {
            return; // the opening interact press must not also press a button
        }

        if (@event.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            WorldSim.Instance.CloseGarageSale();
            return;
        }

        // E falls through the GUI stage (only ui_accept activates a focused button
        // there); route it to the focused button as the same press.
        if (@event.IsActionPressed("interact"))
        {
            if (GetViewport().GuiGetFocusOwner() is not Button focused)
            {
                return;
            }
            if (focused == _buyButton)
            {
                GetViewport().SetInputAsHandled();
                Buy();
            }
            else if (focused == _leaveButton)
            {
                GetViewport().SetInputAsHandled();
                WorldSim.Instance.CloseGarageSale();
            }
        }
    }

    private void BuildControls()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(200, 0) };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        margin.AddChild(vbox);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(header);

        header.AddChild(new Label
        {
            Text = "FOR SALE",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });

        _moneyLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _moneyLabel.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f));
        header.AddChild(_moneyLabel);

        vbox.AddChild(new HSeparator());

        // [KEVIN] placeholder copy — canon restatement only (docs/story/README.md
        // §West entry): the closed repair garage beside the gas station.
        var body = new Label
        {
            Text = "The repair garage beside the gas station.\nClosed.",
        };
        body.AddThemeFontSizeOverride("font_size", 8);
        vbox.AddChild(body);

        vbox.AddChild(new Label { Text = $"Asking: {GarageRules.Price}g" });

        _buyButton = new Button
        {
            Text = "Buy the garage",
            Alignment = HorizontalAlignment.Left,
        };
        // Button.Pressed is a Godot signal (auto-disconnects when the button is
        // freed), unlike the plain C# events unsubscribed in _ExitTree.
        _buyButton.Pressed += Buy;
        vbox.AddChild(_buyButton);

        _leaveButton = new Button
        {
            Text = "Walk away",
            Alignment = HorizontalAlignment.Left,
        };
        _leaveButton.Pressed += () => WorldSim.Instance.CloseGarageSale();
        vbox.AddChild(_leaveButton);

        // Empty text still reserves a line, so refusal flashes never resize the panel.
        _flashLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _flashLabel.AddThemeFontSizeOverride("font_size", 8);
        _flashLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.65f, 0.18f));
        vbox.AddChild(_flashLabel);

        _flashTimer = new Godot.Timer { WaitTime = 0.8, OneShot = true };
        _flashTimer.Timeout += OnFlashTimeout;
        AddChild(_flashTimer);
    }

    private void OnOpened()
    {
        _openedFrame = Engine.GetProcessFrames();
        UpdateMoney(SaveService.Instance.Current.Player.Money);
        _flashLabel.Text = "";
        Visible = true;
        // The safe option holds boot focus — six figures wants a deliberate arrow.
        _leaveButton.GrabFocus();
    }

    private void OnClosed()
    {
        Visible = false; // hiding also releases button focus
    }

    private void OnMoneyChanged(long money) => UpdateMoney(money);

    private void Buy()
    {
        switch (WorldSim.Instance.BuyGarage())
        {
            case GarageSaleResult.Ok:
                // The bus closed the session itself; the panel hiding and the HUD
                // money drop are the feedback (a future quest pair would toast here).
                break;
            case GarageSaleResult.InsufficientFunds:
                Flash("Not enough money");
                break;
            // NotOpen/AlreadyOwned: dead session — nothing useful to tell the player.
        }
    }

    private void UpdateMoney(long money)
    {
        _moneyLabel.Text = $"{money}g";
    }

    private void Flash(string message)
    {
        _flashLabel.Text = message;
        _flashTimer.Start();
    }

    private void OnFlashTimeout()
    {
        _flashLabel.Text = "";
    }
}
