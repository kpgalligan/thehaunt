using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The world's single tint layer (art handoff §6). A CanvasModulate multiplies every
/// canvas item in the world canvas and nothing on the UI layer, which is exactly the
/// "over the world, below the UI" the spec asks for; the lanterns then punch back
/// through it as additive <see cref="GlowLight"/>s.
///
/// Owns no durable state — the tint is a pure function of (map, clock).
/// </summary>
public partial class DayNightTint : CanvasModulate
{
    private bool _interior;

    public override void _Ready()
    {
        Clock.Instance.TenMinuteTicked += OnTimeChanged;
        Clock.Instance.DayStarted += OnTimeChanged;
        Apply();
    }

    public override void _ExitTree()
    {
        Clock.Instance.TenMinuteTicked -= OnTimeChanged;
        Clock.Instance.DayStarted -= OnTimeChanged;
    }

    /// <summary>Called by Main whenever the loaded map changes (boot and travel).</summary>
    public void SetMap(MapRoot map)
    {
        _interior = map.IsInterior;
        Apply();
    }

    private void OnTimeChanged(GameTime time) => Apply();

    private void Apply() =>
        Color = _interior ? DayNight.InteriorAmbient : DayNight.Modulate(Clock.Instance.Now);
}
