using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The barn, in whichever of its three drawn states the story flags put it in
/// (<see cref="BarnRules"/>): derelict, weathertight, restored. Three states, three
/// variants of the same 6x7 sprite side by side on one sheet — swapping is a region
/// change, not a rebuild.
///
/// Only the restored barn is lit. Derelict windows are holes onto darkness and
/// weathertight ones are glazed but nobody is in there yet, so the glow lights come on
/// exactly once, which is the point: finishing the barn is the most visible thing the
/// player will have done, and it is the only saturated red mass in the game.
///
/// A pure view. The map calls <see cref="SetState"/> from ApplyState, which WorldSim
/// already runs on every flag change and every dawn — no event subscription to leak.
/// </summary>
public partial class BarnFacade : Prop
{
    public const string BarnPath = "res://assets/sprites/farm/barn.png";

    public const int Tiles = 6;
    public const int FootprintRows = 5;

    private const int StateWidth = 96;
    private const int StateHeight = 112;

    // Offsets are the lit pixels' centres in the restored variant, measured from the
    // facade's bottom-centre — the same convention the town's facades use. The windows
    // are the only lit pixels there is: the hayloft door and the main doors are painted
    // timber in every state, and the cupola vent is drawn in the SAME cold blue as the
    // weathertight barn's unlit glazing, so a warm light there would sit on top of cold
    // art. Two windows, and that is the barn coming back to life.
    private static readonly (float X, float Y, GlowLight.Falloff Size, float Strength)[] Lights =
    {
        (-29f, -54f, GlowLight.Falloff.Small, 0.5f),   // west window
        (29f, -54f, GlowLight.Falloff.Small, 0.5f),    // east window
    };

    private readonly List<GlowLight> _lights = new();

    /// <summary>Source rect for a repair state; out-of-range clamps to the derelict end.</summary>
    public static Rect2 Variant(int state) =>
        new(Mathf.Clamp(state, BarnRules.Derelict, BarnRules.Restored) * StateWidth, 0,
            StateWidth, StateHeight);

    public override void _Ready()
    {
        base._Ready();
        foreach (var (x, y, size, strength) in Lights)
        {
            var glow = new GlowLight
            {
                Position = new Vector2(x, y),
                Size = size,
                Strength = strength,
                Visible = false,
            };
            _lights.Add(glow);
            AddChild(glow);
        }
    }

    public void SetState(int state)
    {
        RegionRect = Variant(state);
        bool lit = state >= BarnRules.Restored;
        foreach (GlowLight glow in _lights)
            glow.Visible = lit;
    }
}
