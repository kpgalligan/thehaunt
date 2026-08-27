using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Bottom-left stamina bar, no text. Green normally, amber under 25%.
/// Redraws from the model on WorldSim.StaminaChanged / SaveService.AfterLoad.</summary>
public partial class StaminaBar : Control
{
    private const float BarWidth = 60f;
    private const float BarHeight = 8f;
    private const float Inset = 1f; // frame border thickness around the fill

    private static readonly Color GreenFill = new(0.30f, 0.78f, 0.35f);
    private static readonly Color AmberFill = new(0.92f, 0.65f, 0.18f);

    private ColorRect _fill = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var frame = new Panel
        {
            CustomMinimumSize = new Vector2(BarWidth, BarHeight),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var frameStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.06f, 0.7f),
            BorderColor = new Color(1f, 1f, 1f, 0.3f),
        };
        frameStyle.SetBorderWidthAll((int)Inset);
        frame.AddThemeStyleboxOverride("panel", frameStyle);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.Minsize, margin: 8);
        frame.GrowHorizontal = GrowDirection.End;
        frame.GrowVertical = GrowDirection.Begin;
        AddChild(frame);

        _fill = new ColorRect
        {
            Position = new Vector2(Inset, Inset),
            Size = new Vector2(BarWidth - Inset * 2, BarHeight - Inset * 2),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        frame.AddChild(_fill);

        WorldSim.Instance.StaminaChanged += OnStaminaChanged;
        SaveService.Instance.AfterLoad += OnAfterLoad;

        PlayerData player = SaveService.Instance.Current.Player;
        UpdateBar(player.Stamina, player.MaxStamina);
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.StaminaChanged -= OnStaminaChanged;
        SaveService.Instance.AfterLoad -= OnAfterLoad;
    }

    private void OnStaminaChanged(int current, int max) => UpdateBar(current, max);

    private void OnAfterLoad()
    {
        PlayerData player = SaveService.Instance.Current.Player;
        UpdateBar(player.Stamina, player.MaxStamina);
    }

    private void UpdateBar(int current, int max)
    {
        float ratio = max > 0 ? Mathf.Clamp(current / (float)max, 0f, 1f) : 0f;
        _fill.Size = new Vector2((BarWidth - Inset * 2) * ratio, BarHeight - Inset * 2);
        _fill.Color = ratio < 0.25f ? AmberFill : GreenFill;
    }
}
