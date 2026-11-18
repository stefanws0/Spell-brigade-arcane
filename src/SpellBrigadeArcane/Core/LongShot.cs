namespace SpellBrigadeArcane.Core;

using System;

/// <summary>
/// Reach's Mastery "Long Shot": the farther the enemy, the more damage. No bonus up to <see cref="DefaultNear"/>
/// units, rising linearly to <see cref="DefaultMaxBonus"/> at <see cref="DefaultFar"/> units and beyond.
/// For scale: the Dash mod's dash is 3 units.
/// </summary>
public static class LongShot
{
    public const float DefaultNear = 4f;
    public const float DefaultFar = 16f;

    /// <summary>A fraction, not a percent: 0.20 means +20% (unlike Prism's percent scale).</summary>
    public const float DefaultMaxBonus = 0.20f;

    public static float Multiplier(float distance, float near = DefaultNear, float far = DefaultFar, float maxBonus = DefaultMaxBonus)
    {
        if (far <= near) throw new ArgumentException($"far ({far}) must be greater than near ({near})");
        if (!(distance > near)) return 1f;
        if (distance >= far) return 1f + maxBonus;
        return 1f + maxBonus * (distance - near) / (far - near);
    }
}
