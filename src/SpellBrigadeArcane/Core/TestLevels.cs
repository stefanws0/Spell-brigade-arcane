namespace SpellBrigadeArcane.Core;

using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// [Debug] TestLevels, e.g. "Volley=6, Alchemy=3": missions use these levels instead of the bought ones (test aid; it
/// costs no gold and changes no save). Bad parts are reported and skipped; levels are clamped to 0..max.
/// </summary>
public static class TestLevels
{
    public static IReadOnlyDictionary<ArcaneId, int> Parse(string? text, int maxLevel, out List<string> problems)
    {
        problems = new List<string>();
        var levels = new Dictionary<ArcaneId, int>();
        if (string.IsNullOrWhiteSpace(text)) return levels;
        text = text.Trim().Trim('"', '\'');

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || int.TryParse(pair[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || !Enum.TryParse<ArcaneId>(pair[0], true, out var id) || !Enum.IsDefined(id))
            {
                problems.Add($"'{part}' is not <enchantment>=<level>");
                continue;
            }

            if (!int.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
            {
                problems.Add($"'{part}': '{pair[1]}' is not a whole number");
                continue;
            }

            var clamped = Math.Clamp(level, 0, maxLevel);
            if (clamped != level) problems.Add($"'{part}': level {level} is outside 0..{maxLevel}, using {clamped}");
            levels[id] = clamped;
        }

        return levels;
    }
}
