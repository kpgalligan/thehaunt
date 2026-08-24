using Godot;
using TheHaunt.Player;
using TheHaunt.World;

namespace TheHaunt.UI;

/// <summary>Bottom-center "[E] ..." hint that mirrors the interaction probe's focus.</summary>
public partial class InteractionPrompt : Control
{
    private Label _label = null!;
    private InteractionProbe? _probe;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _label = new Label
        {
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, margin: 24);
        _label.GrowHorizontal = GrowDirection.Both;
        _label.GrowVertical = GrowDirection.Begin;
        AddChild(_label);
    }

    /// <summary>Called by Main; unbinds any previous probe.</summary>
    public void Bind(InteractionProbe probe)
    {
        if (_probe != null)
            _probe.FocusChanged -= OnFocusChanged;

        _probe = probe;
        _probe.FocusChanged += OnFocusChanged;
        OnFocusChanged(probe.Focused);
    }

    public override void _ExitTree()
    {
        if (_probe != null)
        {
            _probe.FocusChanged -= OnFocusChanged;
            _probe = null;
        }
    }

    private void OnFocusChanged(IInteractable? focused)
    {
        if (focused == null)
        {
            _label.Visible = false;
            return;
        }

        _label.Text = $"[E] {focused.PromptText}";
        _label.Visible = true;
    }
}
