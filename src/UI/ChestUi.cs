using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Centered chest-transfer panel. Pure view over WorldSim's storage session:
/// shows on StorageOpened, hides on StorageClosed (WorldSim fires a Closed itself when a
/// load lands mid-session, so visibility needs no AfterLoad handling). Chest grid above
/// the 10-slot inventory row; slots are focus Buttons — a press moves the WHOLE stack,
/// partial-on-overflow, and "Full" flashes on total refusal; nothing is ever dropped.
/// Esc (the pause action) closes the session. Sits above DialogueUi and below
/// PauseMenu/ScreenFade in Main's UI layer.</summary>
public partial class ChestUi : Control
{
    private const int Columns = 10;   // hotbar width; chest capacity 20 renders as 2 rows
    private const int SlotSize = 26;
    private const int IconSize = 12;

    private GridContainer _chestGrid = null!;
    private GridContainer _inventoryGrid = null!;
    private Label _flashLabel = null!;
    private Godot.Timer _flashTimer = null!;

    private readonly List<Button> _chestButtons = new();
    private readonly List<Button> _inventoryButtons = new();

    // Engine frame the panel became visible. The interact press that OPENED the chest
    // may still be dispatching this frame — it must not also transfer the focused slot
    // (insurance against input-order variance; DialogueUi's pattern).
    private ulong _openedFrame;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        BuildControls();

        WorldSim.Instance.StorageOpened += OnStorageOpened;
        WorldSim.Instance.StorageClosed += OnStorageClosed;
        WorldSim.Instance.StorageChanged += OnStorageChanged;
        WorldSim.Instance.InventoryChanged += OnInventoryChanged;
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.StorageOpened -= OnStorageOpened;
        WorldSim.Instance.StorageClosed -= OnStorageClosed;
        WorldSim.Instance.StorageChanged -= OnStorageChanged;
        WorldSim.Instance.InventoryChanged -= OnInventoryChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (WorldSim.Instance.OpenStorageId is not string storageId)
        {
            return;
        }
        if (Engine.GetProcessFrames() == _openedFrame)
        {
            return; // the opening interact press must not transfer slot 0
        }

        if (@event.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            WorldSim.Instance.CloseStorage();
            return;
        }

        // E falls through the GUI stage (only ui_accept activates a focused button
        // there); route it to the focused slot as the same whole-stack move.
        if (@event.IsActionPressed("interact"))
        {
            if (GetViewport().GuiGetFocusOwner() is not Button focused)
            {
                return;
            }
            int index = _chestButtons.IndexOf(focused);
            if (index >= 0)
            {
                GetViewport().SetInputAsHandled();
                TransferFromChest(storageId, index);
                return;
            }
            index = _inventoryButtons.IndexOf(focused);
            if (index >= 0)
            {
                GetViewport().SetInputAsHandled();
                TransferFromInventory(storageId, index);
            }
        }
    }

    private void BuildControls()
    {
        var panel = new PanelContainer();
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

        vbox.AddChild(new Label { Text = "Chest", HorizontalAlignment = HorizontalAlignment.Center });

        _chestGrid = new GridContainer { Columns = Columns };
        _chestGrid.AddThemeConstantOverride("h_separation", 2);
        _chestGrid.AddThemeConstantOverride("v_separation", 2);
        vbox.AddChild(_chestGrid);

        vbox.AddChild(new HSeparator());

        _inventoryGrid = new GridContainer { Columns = Columns };
        _inventoryGrid.AddThemeConstantOverride("h_separation", 2);
        _inventoryGrid.AddThemeConstantOverride("v_separation", 2);
        vbox.AddChild(_inventoryGrid);

        // Empty text still reserves a line, so refusal flashes never resize the panel.
        _flashLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _flashLabel.AddThemeFontSizeOverride("font_size", 10);
        _flashLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.65f, 0.18f));
        vbox.AddChild(_flashLabel);

        _flashTimer = new Godot.Timer { WaitTime = 0.8, OneShot = true };
        // Timer.Timeout is a Godot signal (auto-disconnects when the timer is freed
        // with this panel), unlike the plain C# events unsubscribed in _ExitTree.
        _flashTimer.Timeout += OnFlashTimeout;
        AddChild(_flashTimer);
    }

    private void OnStorageOpened(string storageId)
    {
        _openedFrame = Engine.GetProcessFrames();
        RebuildSlots(storageId);
        _flashLabel.Text = "";
        Visible = true;
        if (_chestButtons.Count > 0)
        {
            _chestButtons[0].GrabFocus(); // GridContainer geometry gives arrow-key neighbors
        }
    }

    private void OnStorageClosed()
    {
        Visible = false; // hiding also releases any slot button's focus
    }

    private void OnStorageChanged(string storageId)
    {
        if (Visible && storageId == WorldSim.Instance.OpenStorageId)
        {
            RepaintChest();
        }
    }

    private void OnInventoryChanged()
    {
        if (Visible)
        {
            RepaintInventory();
        }
    }

    // Slot buttons are rebuilt per open: the slot count comes from the model (the known
    // chest arrives normalized to capacity; an over-capacity save keeps its extra slots,
    // never trimmed — they simply render as more rows).
    private void RebuildSlots(string storageId)
    {
        ClearGrid(_chestGrid, _chestButtons);
        ClearGrid(_inventoryGrid, _inventoryButtons);

        StorageData storage = SaveService.Instance.Current.GetStorage(storageId);
        for (int i = 0; i < storage.Slots.Count; i++)
        {
            int index = i; // captured per button
            Button button = MakeSlotButton();
            // Button.Pressed is a Godot signal (auto-disconnects when the button is
            // freed), unlike the plain C# events unsubscribed in _ExitTree.
            button.Pressed += () =>
            {
                if (WorldSim.Instance.OpenStorageId is string id)
                {
                    TransferFromChest(id, index);
                }
            };
            _chestGrid.AddChild(button);
            _chestButtons.Add(button);
        }

        for (int i = 0; i < InventoryData.Capacity; i++)
        {
            int index = i;
            Button button = MakeSlotButton();
            button.Pressed += () =>
            {
                if (WorldSim.Instance.OpenStorageId is string id)
                {
                    TransferFromInventory(id, index);
                }
            };
            _inventoryGrid.AddChild(button);
            _inventoryButtons.Add(button);
        }

        RepaintChest();
        RepaintInventory();
    }

    // Whole-stack move, chest -> inventory. What fits moves (WorldSim's partial-on-
    // overflow ordering); false with a non-empty source means nothing fit at all.
    private void TransferFromChest(string storageId, int index)
    {
        StorageData storage = SaveService.Instance.Current.GetStorage(storageId);
        if (index >= storage.Slots.Count || storage.Slots[index] is null)
        {
            return; // pressing an empty slot is a silent no-op, not a refusal
        }
        if (!WorldSim.Instance.TransferToInventory(storageId, index))
        {
            Flash("Full");
        }
    }

    private void TransferFromInventory(string storageId, int index)
    {
        if (SaveService.Instance.Current.Player.Inventory.SlotAt(index) is null)
        {
            return;
        }
        if (!WorldSim.Instance.TransferToStorage(storageId, index))
        {
            Flash("Full");
        }
    }

    private void RepaintChest()
    {
        if (WorldSim.Instance.OpenStorageId is not string storageId)
        {
            return;
        }
        StorageData storage = SaveService.Instance.Current.GetStorage(storageId);
        for (int i = 0; i < _chestButtons.Count; i++)
        {
            PaintSlot(_chestButtons[i], i < storage.Slots.Count ? storage.Slots[i] : null);
        }
    }

    private void RepaintInventory()
    {
        InventoryData inventory = SaveService.Instance.Current.Player.Inventory;
        for (int i = 0; i < _inventoryButtons.Count; i++)
        {
            PaintSlot(_inventoryButtons[i], inventory.SlotAt(i));
        }
    }

    private static void PaintSlot(Button button, ItemStackRecord? stack)
    {
        var icon = button.GetChild<ColorRect>(0);
        var glyph = button.GetChild<Label>(1);
        var count = button.GetChild<Label>(2);

        if (stack is null)
        {
            icon.Visible = false;
            glyph.Visible = false;
            count.Visible = false;
            return;
        }

        ItemDef? def = ItemDefs.TryGet(stack.ItemId);
        icon.Visible = true;
        icon.Color = def is not null && Color.HtmlIsValid(def.IconColor)
            ? Color.FromHtml(def.IconColor)
            : new Color(0.42f, 0.42f, 0.42f); // unknown id: gray '?', preserved never dropped
        glyph.Visible = def is null;
        count.Text = stack.Count.ToString();
        count.Visible = stack.Count > 1;
    }

    private static Button MakeSlotButton()
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(SlotSize, SlotSize),
            FocusMode = FocusModeEnum.All,
        };

        // Child order is PaintSlot's contract: 0 = icon, 1 = '?' glyph, 2 = count.
        var icon = new ColorRect
        {
            CustomMinimumSize = new Vector2(IconSize, IconSize),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
        icon.GrowHorizontal = GrowDirection.Both;
        icon.GrowVertical = GrowDirection.Both;
        button.AddChild(icon);

        var glyph = new Label
        {
            Text = "?",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        glyph.AddThemeFontSizeOverride("font_size", 10);
        glyph.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        button.AddChild(glyph);

        var count = new Label
        {
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        count.AddThemeFontSizeOverride("font_size", 8);
        count.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        count.AddThemeConstantOverride("outline_size", 2);
        count.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, margin: 1);
        count.GrowHorizontal = GrowDirection.Begin;
        count.GrowVertical = GrowDirection.Begin;
        button.AddChild(count);

        return button;
    }

    private static void ClearGrid(GridContainer grid, List<Button> buttons)
    {
        // RemoveChild before QueueFree so a same-frame reopen never shows dying and
        // new slots together (DialogueUi's choice-rebuild pattern).
        foreach (Node child in grid.GetChildren())
        {
            grid.RemoveChild(child);
            child.QueueFree();
        }
        buttons.Clear();
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
