using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The general store's facade, swapped between the sheet's open and closed variants
/// by <see cref="ShopHours"/> — a warm doorway and lit windows while the counter is
/// open, shutters when it is not. The cheapest possible "is the store open"
/// affordance, and it means the sign never has to be read twice (art handoff §4).
///
/// A pure view: it reads the clock, never writes anything.
/// </summary>
public partial class StoreFacade : Prop
{
    public const string StorePath = "res://assets/sprites/town/building_store.png";

    /// <summary>Both variants are 7x6 tiles; the caller seeds <see cref="Prop.Source"/>
    /// with the open one so the facade anchors at the right height before the first tick.</summary>
    public static readonly Rect2 OpenVariant = new(0, 0, 112, 96);

    private static readonly Rect2 ClosedVariant = new(112, 0, 112, 96);

    private GlowLight? _doorGlow;

    public override void _Ready()
    {
        base._Ready();
        Clock.Instance.TenMinuteTicked += OnTimeChanged;
        Clock.Instance.DayStarted += OnTimeChanged;

        // The doorway's own spill, on only while the counter is. Offsets are the lit
        // pixels' centres in the open variant, measured from the facade's bottom-centre.
        _doorGlow = new GlowLight
        {
            Name = "DoorGlow",
            Position = new Vector2(-0.5f, -41f),
            Size = GlowLight.Falloff.Large,
            Strength = 0.7f,
        };
        AddChild(_doorGlow);

        foreach (var windowX in new[] { -37f, -17f, 37f })
        {
            AddChild(new GlowLight
            {
                Name = $"WindowGlow{windowX}",
                Position = new Vector2(windowX, -43f),
                Size = GlowLight.Falloff.Small,
                Strength = 0.55f,
            });
        }

        ApplyVariant();
    }

    public override void _ExitTree()
    {
        Clock.Instance.TenMinuteTicked -= OnTimeChanged;
        Clock.Instance.DayStarted -= OnTimeChanged;
    }

    private void OnTimeChanged(GameTime time) => ApplyVariant();

    private void ApplyVariant()
    {
        bool open = ShopHours.IsOpen(Clock.Instance.Now.MinuteOfDay);
        RegionRect = open ? OpenVariant : ClosedVariant;
        foreach (Node child in GetChildren())
        {
            if (child is GlowLight glow)
                glow.Visible = open;
        }
    }
}
