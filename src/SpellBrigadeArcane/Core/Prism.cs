namespace SpellBrigadeArcane.Core;

using System;
using System.Collections.Generic;

/// <summary>
/// Attunement's Mastery "Prism": +<see cref="DefaultPercentPerElement"/>% Elemental Damage for every different base
/// element across the equipped spells, up to <see cref="MaxElements"/>. Works on the game's <c>ElementType</c> names:
/// a double ("FireDouble") counts once, a combo ("AcidFire") counts both of its elements.
/// New game ElementType members must be re-checked: matching is by substring of the five base names.
/// </summary>
public static class Prism
{
    public const float DefaultPercentPerElement = 4f;
    public const int MaxElements = 5;

    public static readonly IReadOnlyList<string> BaseElements = new[] { "Fire", "Ice", "Acid", "Lightning", "Dark" };

    public static int DistinctBaseElements(IEnumerable<string> elementNames)
    {
        var found = new HashSet<string>();
        foreach (var name in elementNames)
        {
            if (name == null) continue;
            foreach (var element in BaseElements)
            {
                if (name.Contains(element, StringComparison.Ordinal)) found.Add(element);
            }
        }

        return found.Count;
    }

    public static float BonusPercent(int distinctElements, float percentPerElement = DefaultPercentPerElement) =>
        Math.Clamp(distinctElements, 0, MaxElements) * percentPerElement;
}
