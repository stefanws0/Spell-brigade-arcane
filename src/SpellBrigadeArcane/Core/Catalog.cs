namespace SpellBrigadeArcane.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>The sixth row's enchantments, left to right. The names are save keys (save.json): never rename or remove one.</summary>
public enum ArcaneId
{
    Volley,
    Reach,
    Attunement,
    Alchemy,
}

/// <summary>One game stat an enchantment raises: the game's <c>StatType</c> name and the total at max level, in percent.</summary>
public sealed class StatGain
{
    public StatGain(string stat, float totalPercent)
    {
        Stat = stat;
        TotalPercent = totalPercent;
    }

    public string Stat { get; }

    public float TotalPercent { get; }
}

public sealed class EnchantmentDef
{
    public EnchantmentDef(ArcaneId id, string name, string description, string masteryName, string masteryText, int maxLevel, params StatGain[] gains)
    {
        if (maxLevel < 1) throw new ArgumentOutOfRangeException(nameof(maxLevel), maxLevel, "needs at least one level");
        Id = id;
        Name = name;
        Description = description;
        MasteryName = masteryName;
        MasteryText = masteryText;
        MaxLevel = maxLevel;
        Gains = gains.ToArray();
    }

    public ArcaneId Id { get; }

    public string Name { get; }

    public string Description { get; }

    public string MasteryName { get; }

    public string MasteryText { get; }

    public int MaxLevel { get; }

    public IReadOnlyList<StatGain> Gains { get; }

    /// <summary>Percent of <paramref name="stat"/> given at <paramref name="level"/>: the max-level total spread evenly over the levels.</summary>
    public float PercentAt(string stat, int level)
    {
        var gain = Gains.FirstOrDefault(g => g.Stat == stat);
        if (gain == null) return 0f;
        return gain.TotalPercent * Math.Clamp(level, 0, MaxLevel) / MaxLevel;
    }
}

public static class Catalog
{
    /// <summary>Assumed until the scan reads the game's real level count (spec: keep the totals, divide over the real count).</summary>
    public const int DefaultMaxLevel = 5;

    public static IReadOnlyList<EnchantmentDef> Build(int maxLevel = DefaultMaxLevel) => new[]
    {
        new EnchantmentDef(ArcaneId.Volley, "Volley",
            "Increase your <b>Projectile Speed</b>, so your projectiles reach enemies sooner.",
            "+1 Projectile",
            "All your Spells fire <b>1 extra Projectile</b>.",
            maxLevel, new StatGain("Speed", 30f)),
        new EnchantmentDef(ArcaneId.Reach, "Reach",
            "Increase your <b>Spell Range</b>, so you can hit enemies from farther away.",
            "Long Shot",
            FormattableString.Invariant($"You deal up to <b>{LongShot.DefaultMaxBonus * 100f:0}% more Damage</b> the farther away an enemy is."),
            maxLevel, new StatGain("Range", 30f)),
        new EnchantmentDef(ArcaneId.Attunement, "Attunement",
            "Increase your <b>Elemental Damage</b>, so your infused Spells hit harder.",
            "Prism",
            FormattableString.Invariant($"Your Elemental Damage is increased by <b>{Prism.DefaultPercentPerElement:0}%</b> for every different Element across your Spells."),
            maxLevel, new StatGain("ElementStrengthMultiplier", 25f)),
        new EnchantmentDef(ArcaneId.Alchemy, "Alchemy",
            "<b>Health Potions</b> heal more and drop more often.",
            "Potion Belt",
            FormattableString.Invariant($"A Health Potion picked up at full Health is <b>saved</b> and drunk automatically when your Health drops below <b>{PotionBelt.DefaultThreshold * 100f:0}%</b>."),
            maxLevel, new StatGain("HealthPotionAmountMultiplier", 50f), new StatGain("DropChance", 25f)),
    };

    public static EnchantmentDef Get(IReadOnlyList<EnchantmentDef> all, ArcaneId id) => all.First(d => d.Id == id);
}
