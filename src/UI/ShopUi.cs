using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Centered store panel. Pure view over WorldSim's shop session: shows on
/// ShopOpened, hides on ShopClosed (WorldSim fires a Closed itself when a load lands
/// mid-session). Catalog rows are focus Buttons — interact/ui_accept/click buys 1,
/// Shift held buys 5, all-or-nothing per WorldSim.BuyItem's checks-before-mutation;
/// refusals flash beneath the rows. Esc (the pause action) closes the session.</summary>
public partial class ShopUi : Control
{
    private Label _moneyLabel = null!;
    private VBoxContainer _rows = null!;
    private Label _flashLabel = null!;
    private Godot.Timer _flashTimer = null!;

    private readonly List<Button> _rowButtons = new();
    private IReadOnlyList<ShopEntry>? _entries;

    // Engine frame the panel became visible. The interact press that OPENED the shop
    // may still be dispatching this frame — it must not also buy row 0 (insurance
    // against input-order variance; DialogueUi's pattern).
    private ulong _openedFrame;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        BuildControls();

        WorldSim.Instance.ShopOpened += OnShopOpened;
        WorldSim.Instance.ShopClosed += OnShopClosed;
        WorldSim.Instance.InventoryChanged += OnInventoryChanged;
        WorldSim.Instance.MoneyChanged += OnMoneyChanged;
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.ShopOpened -= OnShopOpened;
        WorldSim.Instance.ShopClosed -= OnShopClosed;
        WorldSim.Instance.InventoryChanged -= OnInventoryChanged;
        WorldSim.Instance.MoneyChanged -= OnMoneyChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (WorldSim.Instance.OpenShopId is null)
        {
            return;
        }
        if (Engine.GetProcessFrames() == _openedFrame)
        {
            return; // the opening interact press must not buy row 0
        }

        if (@event.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            WorldSim.Instance.CloseShop();
            return;
        }

        // E falls through the GUI stage (only ui_accept activates a focused button
        // there); route it to the focused row as the same buy.
        if (@event.IsActionPressed("interact"))
        {
            if (GetViewport().GuiGetFocusOwner() is not Button focused)
            {
                return;
            }
            int index = _rowButtons.IndexOf(focused);
            if (index >= 0)
            {
                GetViewport().SetInputAsHandled();
                Buy(index);
            }
        }
    }

    private void BuildControls()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(180, 0) };
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

        // [KEVIN] Role label, not a name — the store's NAME is deliberately unnamed.
        header.AddChild(new Label
        {
            Text = "General Store",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });

        _moneyLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _moneyLabel.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f));
        header.AddChild(_moneyLabel);

        vbox.AddChild(new HSeparator());

        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(_rows);

        var hint = new Label { Text = "Shift — buy 5", HorizontalAlignment = HorizontalAlignment.Right };
        hint.AddThemeFontSizeOverride("font_size", 8);
        vbox.AddChild(hint);

        // Empty text still reserves a line, so refusal flashes never resize the panel.
        _flashLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _flashLabel.AddThemeFontSizeOverride("font_size", 8);
        _flashLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.65f, 0.18f));
        vbox.AddChild(_flashLabel);

        _flashTimer = new Godot.Timer { WaitTime = 0.8, OneShot = true };
        // Timer.Timeout is a Godot signal (auto-disconnects when the timer is freed
        // with this panel), unlike the plain C# events unsubscribed in _ExitTree.
        _flashTimer.Timeout += OnFlashTimeout;
        AddChild(_flashTimer);
    }

    private void OnShopOpened(string catalogId)
    {
        _openedFrame = Engine.GetProcessFrames();
        _entries = ShopCatalog.TryGet(catalogId); // OpenShop validated it — never null here
        RebuildRows();
        UpdateMoney(SaveService.Instance.Current.Player.Money);
        _flashLabel.Text = "";
        Visible = true;
        if (_rowButtons.Count > 0)
        {
            _rowButtons[0].GrabFocus(); // VBox order gives ui_up/ui_down neighbors automatically
        }
    }

    private void OnShopClosed()
    {
        Visible = false; // hiding also releases any row button's focus
        _entries = null;
    }

    private void OnInventoryChanged()
    {
        if (Visible)
        {
            RepaintRows(); // "(have n)" counts
        }
    }

    private void OnMoneyChanged(long money) => UpdateMoney(money);

    private void RebuildRows()
    {
        // RemoveChild before QueueFree so a same-frame reopen never shows dying and
        // new rows together (DialogueUi's choice-rebuild pattern).
        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }
        _rowButtons.Clear();

        if (_entries is null)
        {
            return;
        }
        for (int i = 0; i < _entries.Count; i++)
        {
            int index = i; // captured per button
            var button = new Button
            {
                Text = RowText(_entries[i]),
                Alignment = HorizontalAlignment.Left,
            };
            // Button.Pressed is a Godot signal (auto-disconnects when the button is
            // freed), unlike the plain C# events unsubscribed in _ExitTree.
            button.Pressed += () => Buy(index);
            _rows.AddChild(button);
            _rowButtons.Add(button);
        }
    }

    private void RepaintRows()
    {
        if (_entries is null)
        {
            return;
        }
        for (int i = 0; i < _rowButtons.Count && i < _entries.Count; i++)
        {
            _rowButtons[i].Text = RowText(_entries[i]);
        }
    }

    private static string RowText(ShopEntry entry)
    {
        // Unknown catalog ids render as '?' like every other unknown-id surface
        // (a validation test pins the shipped catalogs resolving, so never in practice).
        string name = ItemDefs.TryGet(entry.ItemId)?.Name ?? "?";
        int have = SaveService.Instance.Current.Player.Inventory.CountOf(entry.ItemId);
        return $"{name} — {entry.BuyPrice}g (have {have})";
    }

    private void Buy(int index)
    {
        if (_entries is null || index >= _entries.Count)
        {
            return;
        }

        // Shift buys 5, all-or-nothing: BuyItem validates funds AND room strictly
        // before any mutation, so a failed 5-buy touches nothing.
        int count = Input.IsKeyPressed(Key.Shift) ? 5 : 1;
        switch (WorldSim.Instance.BuyItem(_entries[index].ItemId, count))
        {
            case BuyResult.Ok:
                _flashLabel.Text = ""; // repaint arrives via MoneyChanged + InventoryChanged
                break;
            case BuyResult.InsufficientFunds:
                Flash("Not enough money");
                break;
            case BuyResult.NoRoom:
                Flash("Inventory full");
                break;
            // UnknownItem: dead session or catalog bug — nothing useful to tell the player.
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
