namespace SpellBrigadeArcane.Core;

using System;
using System.Collections.Generic;

/// <summary>Where a bonus goes in a mission.</summary>
public enum BonusTarget
{
    /// <summary>A character stat, sent through the game's own network message like the game's enchantments.</summary>
    Character,

    /// <summary>Each equipped spell's own stat: Speed and Range are not character stats in this game.</summary>
    EverySpell,
}

/// <summary>How a game stat combines its modifiers; the names match the game's <c>StatCalculationType</c>.</summary>
public enum StatCalc
{
    PercentAdd,
    Flat,
    PercentAddLogarithmic,
    Unknown,
}

/// <summary>One bonus for a mission. <see cref="Amount"/> is in percent when <see cref="IsPercent"/>, else in game units.</summary>
public sealed record StatBonus(string Stat, float Amount, bool IsPercent, BonusTarget Target, string Source);

/// <summary>Turns owned levels into the stat bonuses a mission gets. Prism and Potion Belt run during the mission.</summary>
public static class RunBonuses
{
    public const string ProjectilesStat = "Projectiles";

    /// <summary>Stats the game doesn't keep on the character (research, 19 Nov): added to every equipped spell instead.</summary>
    public static readonly IReadOnlySet<string> SpellOnlyStats = new HashSet<string> { "Speed", "Range" };

    /// <summary>Every stat level's gains, plus Volley's Mastery (+1 Projectile, a flat character stat).</summary>
    public static IReadOnlyList<StatBonus> ForLevels(IReadOnlyList<EnchantmentDef> defs, Func<ArcaneId, int> levelOf)
    {
        var bonuses = new List<StatBonus>();
        foreach (var def in defs)
        {
            var level = Math.Clamp(levelOf(def.Id), 0, def.MaxLevel);
            if (level == 0) continue;
            foreach (var gain in def.Gains)
            {
                var percent = def.PercentAt(gain.Stat, level);
                if (!(percent > 0f)) continue;
                var target = SpellOnlyStats.Contains(gain.Stat) ? BonusTarget.EverySpell : BonusTarget.Character;
                bonuses.Add(new StatBonus(gain.Stat, percent, true, target, $"{def.Name} {level}"));
            }

            if (def.Id == ArcaneId.Volley && def.HasMastery(level))
                bonuses.Add(new StatBonus(ProjectilesStat, 1f, false, BonusTarget.Character, "Volley Mastery"));
        }

        return bonuses;
    }

    /// <summary>
    /// The modifier value to hand the game for a bonus on a stat with this calculation type and base value, or null when
    /// it can't be expressed safely. PercentAdd: the game computes base × (1 + sum / 100), so percent points go in as is.
    /// Flat: base × percent / 100 gives the same relative change. A flat bonus (Projectiles +1) only fits a flat stat.
    /// </summary>
    public static float? ModifierValue(StatBonus bonus, StatCalc calc, float baseValue)
    {
        if (!bonus.IsPercent) return calc == StatCalc.Flat ? bonus.Amount : null;
        return calc switch
        {
            StatCalc.PercentAdd => bonus.Amount,
            StatCalc.Flat when float.IsFinite(baseValue) && baseValue != 0f => baseValue * bonus.Amount / 100f,
            _ => null,
        };
    }

    /// <summary>The game's StatCalculationType name (from <c>ToString()</c>) as a <see cref="StatCalc"/>; Unknown if new.</summary>
    public static StatCalc ParseCalc(string? name) =>
        name != null && Enum.TryParse<StatCalc>(name, false, out var calc) && Enum.IsDefined(calc) ? calc : StatCalc.Unknown;
}
