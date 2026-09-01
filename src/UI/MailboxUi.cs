using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Centered mail panel. Pure view over WorldSim's mailbox session: shows on
/// MailboxOpened, hides on MailboxClosed (WorldSim fires a Closed itself when a load
/// lands mid-session). Letter list on the left (unread letters carry a dot); opening
/// a letter (E / click) puts its body up and stamps its ReadFlag through the bus —
/// which is what lowers the mailbox's raised flag and hands out any quest the letter
/// starts. A package letter grows a take-button under the body; "Full" flashes on
/// refusal, and nothing is ever lost. Esc (the pause action) closes the session.
/// Sits above ShopUi and below OvernightReport/PauseMenu/ScreenFade in Main's UI layer.</summary>
public partial class MailboxUi : Control
{
    private VBoxContainer _letterList = null!;
    private Label _titleLabel = null!;
    private Label _bodyLabel = null!;
    private Button _takeButton = null!;
    private Label _takenNote = null!;
    private Label _flashLabel = null!;
    private Godot.Timer _flashTimer = null!;

    private readonly List<Button> _letterButtons = new();
    private readonly List<string> _letterIds = new();
    private string? _shownLetterId;

    // Engine frame the panel became visible. The interact press that OPENED the
    // mailbox may still be dispatching this frame — it must not also open the
    // focused letter (DialogueUi's pattern).
    private ulong _openedFrame;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        BuildControls();

        WorldSim.Instance.MailboxOpened += OnMailboxOpened;
        WorldSim.Instance.MailboxClosed += OnMailboxClosed;
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.MailboxOpened -= OnMailboxOpened;
        WorldSim.Instance.MailboxClosed -= OnMailboxClosed;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!WorldSim.Instance.MailboxOpen)
        {
            return;
        }
        if (Engine.GetProcessFrames() == _openedFrame)
        {
            return; // the opening interact press must not open a letter
        }

        if (@event.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            WorldSim.Instance.CloseMailbox();
            return;
        }

        // E falls through the GUI stage (only ui_accept activates a focused button
        // there); route it to whichever button holds focus. Space is bound to BOTH
        // interact and ui_accept — skip it here or the GUI-stage activation on its
        // release would fire the same button a second time.
        if (@event.IsActionPressed("interact") && !@event.IsActionPressed("ui_accept"))
        {
            if (GetViewport().GuiGetFocusOwner() is not Button focused)
            {
                return;
            }
            int index = _letterButtons.IndexOf(focused);
            if (index >= 0)
            {
                GetViewport().SetInputAsHandled();
                ShowLetter(_letterIds[index]);
                return;
            }
            if (focused == _takeButton)
            {
                GetViewport().SetInputAsHandled();
                TakePackage();
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

        vbox.AddChild(new Label { Text = "Mailbox", HorizontalAlignment = HorizontalAlignment.Center });

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(hbox);

        _letterList = new VBoxContainer { CustomMinimumSize = new Vector2(110, 0) };
        _letterList.AddThemeConstantOverride("separation", 2);
        hbox.AddChild(_letterList);

        var reading = new VBoxContainer { CustomMinimumSize = new Vector2(300, 150) };
        reading.AddThemeConstantOverride("separation", 4);
        hbox.AddChild(reading);

        _titleLabel = new Label();
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f));
        reading.AddChild(_titleLabel);

        _bodyLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _bodyLabel.AddThemeFontSizeOverride("font_size", 8);
        reading.AddChild(_bodyLabel);

        _takeButton = new Button { Visible = false, FocusMode = FocusModeEnum.All };
        _takeButton.Pressed += TakePackage;
        reading.AddChild(_takeButton);

        _takenNote = new Label { Visible = false, Text = "Package taken." };
        _takenNote.AddThemeFontSizeOverride("font_size", 8);
        _takenNote.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.5f));
        reading.AddChild(_takenNote);

        // Always present with empty text so refusal flashes never resize the panel
        // (ChestUi's pattern).
        _flashLabel = new Label { Text = "" };
        _flashLabel.AddThemeFontSizeOverride("font_size", 8);
        _flashLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.65f, 0.18f));
        reading.AddChild(_flashLabel);

        var hint = new Label
        {
            Text = "E open letter · Esc close",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        hint.AddThemeFontSizeOverride("font_size", 8);
        hint.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.5f));
        vbox.AddChild(hint);

        _flashTimer = new Godot.Timer { WaitTime = 0.8, OneShot = true };
        _flashTimer.Timeout += () => _flashLabel.Text = "";
        AddChild(_flashTimer);
    }

    private void OnMailboxOpened()
    {
        _openedFrame = Engine.GetProcessFrames();
        _shownLetterId = null;
        _titleLabel.Text = "";
        _bodyLabel.Text = "Select a letter.";
        _takeButton.Visible = false;
        _takenNote.Visible = false;
        _flashLabel.Text = "";
        RebuildLetterList();
        Visible = true;
        if (_letterButtons.Count > 0)
        {
            _letterButtons[0].GrabFocus();
        }
    }

    private void OnMailboxClosed() => Visible = false;

    private void RebuildLetterList()
    {
        foreach (Node child in _letterList.GetChildren())
        {
            _letterList.RemoveChild(child);
            child.QueueFree();
        }
        _letterButtons.Clear();
        _letterIds.Clear();

        GameData data = SaveService.Instance.Current;
        foreach (LetterDef letter in MailRules.Delivered(data, Clock.Instance.Now))
        {
            string marker = MailRules.IsRead(letter, data) ? "" : "● ";
            var button = new Button
            {
                Text = marker + letter.Title,
                FocusMode = FocusModeEnum.All,
                Alignment = HorizontalAlignment.Left,
            };
            button.AddThemeFontSizeOverride("font_size", 8);
            string id = letter.Id;
            button.Pressed += () => ShowLetter(id);
            _letterList.AddChild(button);
            _letterButtons.Add(button);
            _letterIds.Add(id);
        }
        if (_letterButtons.Count == 0)
        {
            var empty = new Label { Text = "No mail." };
            empty.AddThemeFontSizeOverride("font_size", 8);
            empty.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.5f));
            _letterList.AddChild(empty);
        }
    }

    private void ShowLetter(string letterId)
    {
        if (LetterDefs.TryGet(letterId) is not { } letter)
        {
            return;
        }
        _shownLetterId = letterId;
        _titleLabel.Text = letter.Title;
        _bodyLabel.Text = letter.Body;
        // Displaying the body IS reading it — the stamp starts any quest the letter
        // hands out and lowers the mailbox flag (a repaint the read itself triggers).
        WorldSim.Instance.ReadLetter(letterId);
        RefreshPackageRow(letter);
        RebuildLetterList();   // the unread dot drops off
        // The rebuild freed the focused row — re-grab or every later key press dies
        // against a null focus owner. A waiting package gets the take-button (the
        // only keyboard path to it); otherwise the opened letter's fresh row.
        if (_takeButton.Visible)
        {
            _takeButton.GrabFocus();
        }
        else
        {
            int index = _letterIds.IndexOf(letterId);
            if (index >= 0)
            {
                _letterButtons[index].GrabFocus();
            }
        }
    }

    private void RefreshPackageRow(LetterDef letter)
    {
        GameData data = SaveService.Instance.Current;
        bool untaken = MailRules.HasUntakenItems(letter, data);
        _takeButton.Visible = untaken;
        _takenNote.Visible = letter.Items is { Count: > 0 } && !untaken;
        if (untaken)
        {
            var parts = new List<string>();
            foreach (LetterItem item in letter.Items!)
            {
                string name = ItemDefs.TryGet(item.ItemId)?.Name ?? item.ItemId;
                parts.Add($"{name} x{item.Count}");
            }
            _takeButton.Text = $"Take package ({string.Join(", ", parts)})";
        }
    }

    private void TakePackage()
    {
        if (_shownLetterId is not { } letterId || LetterDefs.TryGet(letterId) is not { } letter)
        {
            return;
        }
        MailOutcome outcome = WorldSim.Instance.TakeLetterItems(letterId);
        if (outcome == MailOutcome.NoRoom)
        {
            _flashLabel.Text = "Full";
            _flashTimer.Start();
            return;
        }
        RefreshPackageRow(letter);
        if (!_takeButton.Visible && _letterButtons.Count > 0)
        {
            _letterButtons[0].GrabFocus();   // the vanished button must not strand focus
        }
    }
}
