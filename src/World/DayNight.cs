using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The lighting model from the art handoff §6: all art is authored at the midday
/// values, and every other hour is one full-screen tint over the world. Keys are
/// lerped by minute-of-day; nothing here reads the clock, so it stays testable.
///
/// Minute-of-day 0 is 6:00 AM (<see cref="GameTime"/>), so 09:00 = 180, 18:00 = 720,
/// and the clock's 1:59 AM clamp is 1199.
/// </summary>
public static class DayNight
{
    /// <summary>
    /// A tint key. <paramref name="Overlay"/> keys are authored as an overlay blend;
    /// a CanvasModulate can only multiply, so those are applied luminance-preserving
    /// (see <see cref="Modulate"/>) — dawn warms without darkening.
    /// </summary>
    private readonly record struct Key(int Minute, string Tint, float Amount, bool Overlay);

    private static readonly Key[] Keys =
    {
        new(0,    "d8a878", 0.22f, true),   // 06:00 dawn  — low sun
        new(180,  "ffffff", 0f,    false),  // 09:00 day   — the reference state
        new(600,  "ffffff", 0f,    false),  // 16:00 day
        new(720,  "c4703f", 0.30f, false),  // 18:00 dusk  — lanterns light here
        new(840,  "3f4d7a", 0.42f, false),  // 20:00 evening — blue hour
        new(1020, "232a4a", 0.58f, false),  // 23:00 night — lantern only
        new(1140, "1b1e33", 0.66f, false),  // 01:00 late  — the clock's clamp hour
    };

    // Lamp/window energy, keyed independently of the tint so the lanterns light at
    // dusk rather than tracking the alpha curve out of the dawn key.
    private static readonly (int Minute, float Level)[] LightKeys =
    {
        (0, 0.35f),      // 06:00 — last of the night's lanterns
        (150, 0f),       // 08:30
        (600, 0f),       // 16:00
        (720, 0.55f),    // 18:00 dusk
        (840, 0.80f),    // 20:00
        (1020, 1f),      // 23:00
    };

    /// <summary>Interiors never take the day tint — they get one fixed warm key, and
    /// that contrast is what makes stepping inside at dusk feel like relief.</summary>
    public static readonly Color InteriorAmbient = new(1f, 0.96f, 0.89f);

    /// <summary>The world CanvasModulate color for a time of day.</summary>
    public static Color Modulate(GameTime now) => Modulate(now.MinuteOfDay);

    public static Color Modulate(int minuteOfDay)
    {
        (Key from, Key to, float t) = Bracket(minuteOfDay);
        return Resolve(from).Lerp(Resolve(to), t);
    }

    /// <summary>Lantern/window light energy, 0 at midday to 1 in the small hours.</summary>
    public static float LightLevel(GameTime now) => LightLevel(now.MinuteOfDay);

    public static float LightLevel(int minuteOfDay)
    {
        if (minuteOfDay <= LightKeys[0].Minute)
            return LightKeys[0].Level;
        for (int i = 1; i < LightKeys.Length; i++)
        {
            if (minuteOfDay > LightKeys[i].Minute)
                continue;
            var (m0, l0) = LightKeys[i - 1];
            var (m1, l1) = LightKeys[i];
            return Mathf.Lerp(l0, l1, (float)(minuteOfDay - m0) / (m1 - m0));
        }
        return LightKeys[^1].Level;
    }

    private static (Key From, Key To, float T) Bracket(int minuteOfDay)
    {
        if (minuteOfDay <= Keys[0].Minute)
            return (Keys[0], Keys[0], 0f);
        for (int i = 1; i < Keys.Length; i++)
        {
            if (minuteOfDay > Keys[i].Minute)
                continue;
            float t = (float)(minuteOfDay - Keys[i - 1].Minute) / (Keys[i].Minute - Keys[i - 1].Minute);
            return (Keys[i - 1], Keys[i], t);
        }
        return (Keys[^1], Keys[^1], 0f);
    }

    // A multiply of `amount` toward the key's tint. Overlay keys are scaled back up to
    // their own luminance — as far as a multiply can go without any channel passing 1 —
    // so dawn reads as a warm cast rather than a dimming.
    private static Color Resolve(Key key)
    {
        var tint = new Color(key.Tint);
        Color multiply = Colors.White.Lerp(tint, key.Amount);
        if (!key.Overlay)
            return multiply;

        float luminance = 0.2126f * multiply.R + 0.7152f * multiply.G + 0.0722f * multiply.B;
        float brightest = Mathf.Max(multiply.R, Mathf.Max(multiply.G, multiply.B));
        if (luminance <= 0.001f || brightest <= 0.001f)
            return multiply;
        float scale = Mathf.Min(1f / luminance, 1f / brightest);
        return new Color(multiply.R * scale, multiply.G * scale, multiply.B * scale);
    }
}
