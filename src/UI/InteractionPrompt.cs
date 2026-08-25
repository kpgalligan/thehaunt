using Godot;
using TheHaunt.Player;
using TheHaunt.Systems;
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
        // 56 (not 24) so the prompt clears the hotbar along the bottom edge.
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, margin: 56);
        _label.GrowHorizontal = GrowDirection.Both;
        _label.GrowVertical = GrowDirection.Begin;
        AddChild(_label);

        GameState.Instance.StateChanged += OnStateChanged;
        Clock.Instance.MinuteTicked += OnMinuteTicked;
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
        GameState.Instance.StateChanged -= OnStateChanged;
        Clock.Instance.MinuteTicked -= OnMinuteTicked;

        if (_probe != null)
        {
            _probe.FocusChanged -= OnFocusChanged;
            _probe = null;
        }
    }

    // Hidden for the whole span of Dialogue/Cutscene/Sleeping; on regaining control,
    // re-derive from the probe's current focus (FocusChanged fires only on CHANGE, and
    // focus rarely changes while the player is frozen mid-conversation).
    private void OnStateChanged(GameState.Phase from, GameState.Phase to)
    {
        if (!GameState.Instance.PlayerHasControl)
        {
            _label.Visible = false;
        }
        else
        {
            OnFocusChanged(_probe?.Focused);
        }
    }

    // Prompts can be time-varying (ShopCounter flips Shop/Closed at 9:00 and 5:00) while
    // focus never changes; re-derive each display minute. MinuteTicked is the sanctioned
    // display-only event (Hud precedent).
    private void OnMinuteTicked(Core.GameTime _) => OnFocusChanged(_probe?.Focused);

    private void OnFocusChanged(IInteractable? focused)
    {
        // The control gate keeps a mid-cutscene focus change (an NPC synced into probe
        // range while the player is frozen) from re-showing the prompt. The validity
        // guard covers the tick that despawns a focused NpcView via SyncNpcsNow before
        // the probe's next physics poll clears it.
        if (focused is GodotObject obj && !GodotObject.IsInstanceValid(obj))
            focused = null;
        if (focused == null || !GameState.Instance.PlayerHasControl)
        {
            _label.Visible = false;
            return;
        }

        _label.Text = $"[E] {focused.PromptText}";
        _label.Visible = true;
    }
}
