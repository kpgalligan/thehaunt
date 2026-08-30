using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Skill stats, toggled with K ("toggle_skills") — Kevin's "option
/// available from the key menu", read as a key listed in the Tab controls menu
/// beside Quests/J [KEVIN — say the word and it becomes a PauseMenu entry
/// instead]. Non-modal exactly like QuestLogUi: no phase change, the clock keeps
/// running, losing player control force-hides it. Content is a pure derivation
/// (SkillRules over PlayerData.SkillXp): one row per skill in SkillIds.All order,
/// level plus progress toward the next — rebuilt on every show and on every
/// SkillsChanged while visible, so a harvest under an open panel counts up live.</summary>
public partial class SkillsPanel : Control
{
    private VBoxContainer _rows = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        BuildControls();

        GameState.Instance.StateChanged += OnStateChanged;
        WorldSim.Instance.SkillsChanged += OnSkillsChanged;
    }

    public override void _ExitTree()
    {
        GameState.Instance.StateChanged -= OnStateChanged;
        WorldSim.Instance.SkillsChanged -= OnSkillsChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!GameState.Instance.PlayerHasControl)
        {
            return; // leave the press unhandled — the panel only toggles in free play
        }
        if (@event.IsActionPressed("toggle_skills"))
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
        // Bottom-right: the one free corner (HUD top-right, help left-center,
        // quests right-center, stamina bottom-left, hotbar bottom-center), so all
        // three non-modal panels can stand open together without stacking. Lifted
        // 38px so the bottom row clears the hotbar (InteractionPrompt's 46 rule).
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, margin: 8);
        panel.OffsetTop -= 38;
        panel.OffsetBottom -= 38;
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.GrowVertical = GrowDirection.Begin;
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

        var title = new Label { Text = "Skills" };
        title.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f));
        _rows.AddChild(title);

        GameData data = SaveService.Instance.Current;
        foreach (string skillId in SkillIds.All)
        {
            long xp = SkillRules.Xp(data, skillId);
            int level = SkillRules.LevelForXp(xp);
            string progress = level >= SkillRules.MaxLevel
                ? "MAX"
                : $"{SkillRules.XpIntoLevel(xp)}/{SkillRules.XpPerLevel}";
            var row = new Label
            {
                Text = $"{SkillIds.DisplayName(skillId)} — Lv {level}  ({progress})",
                CustomMinimumSize = new Vector2(150, 0),
            };
            row.AddThemeFontSizeOverride("font_size", 8);
            _rows.AddChild(row);
        }
    }

    private void OnSkillsChanged()
    {
        if (Visible)
        {
            Rebuild();   // XP landing under an open panel counts up live
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
