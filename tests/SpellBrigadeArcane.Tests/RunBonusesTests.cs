namespace SpellBrigadeArcane.Tests;

using System.Collections.Generic;
using System.Linq;
using SpellBrigadeArcane.Core;
using Xunit;

public class RunBonusesTests
{
    private static readonly IReadOnlyList<EnchantmentDef> Defs = Catalog.Build();

    private static readonly StatBonus ThirtyPercent = new("Speed", 30f, true, BonusTarget.EverySpell, "test");

    private static IReadOnlyList<StatBonus> For(params (ArcaneId Id, int Level)[] owned) =>
        RunBonuses.ForLevels(Defs, id => owned.Where(o => o.Id == id).Select(o => o.Level).DefaultIfEmpty(0).First());

    [Fact]
    public void NothingOwned_GivesNothing() => Assert.Empty(For());

    [Fact]
    public void Volley3_SpeedOnEverySpell()
    {
        var bonus = Assert.Single(For((ArcaneId.Volley, 3)));
        Assert.Equal("Speed", bonus.Stat);
        Assert.Equal(18f, bonus.Amount, 3);
        Assert.True(bonus.IsPercent);
        Assert.Equal(BonusTarget.EverySpell, bonus.Target);
        Assert.Equal("Volley 3", bonus.Source);
    }

    [Fact]
    public void Volley5_NoExtraProjectileYet() =>
        Assert.DoesNotContain(For((ArcaneId.Volley, 5)), b => b.Stat == RunBonuses.ProjectilesStat);

    [Fact]
    public void Volley6_FullSpeedAndOneProjectile()
    {
        var all = For((ArcaneId.Volley, 6));
        Assert.Equal(2, all.Count);
        Assert.Equal(30f, all[0].Amount, 3);
        var projectile = all[1];
        Assert.Equal(RunBonuses.ProjectilesStat, projectile.Stat);
        Assert.Equal(1f, projectile.Amount);
        Assert.False(projectile.IsPercent);
        Assert.Equal(BonusTarget.Character, projectile.Target);
        Assert.Equal("Volley Mastery", projectile.Source);
    }

    [Fact]
    public void Reach1_RangeOnEverySpell()
    {
        var bonus = Assert.Single(For((ArcaneId.Reach, 1)));
        Assert.Equal("Range", bonus.Stat);
        Assert.Equal(6f, bonus.Amount, 3);
        Assert.Equal(BonusTarget.EverySpell, bonus.Target);
    }

    [Fact]
    public void Attunement2_ElementalDamageOnTheCharacter()
    {
        var bonus = Assert.Single(For((ArcaneId.Attunement, 2)));
        Assert.Equal("ElementStrengthMultiplier", bonus.Stat);
        Assert.Equal(10f, bonus.Amount, 3);
        Assert.Equal(BonusTarget.Character, bonus.Target);
    }

    [Fact]
    public void Alchemy5_BothPotionStatsOnTheCharacter()
    {
        var all = For((ArcaneId.Alchemy, 5));
        Assert.Equal(2, all.Count);
        Assert.Contains(all, b => b.Stat == "HealthPotionAmountMultiplier" && System.Math.Abs(b.Amount - 50f) < 0.001f && b.Target == BonusTarget.Character);
        Assert.Contains(all, b => b.Stat == "DropChance" && System.Math.Abs(b.Amount - 25f) < 0.001f && b.Target == BonusTarget.Character);
    }

    [Fact]
    public void LevelAboveMax_IsClamped()
    {
        var all = For((ArcaneId.Volley, 99));
        Assert.Equal(30f, all[0].Amount, 3);
        Assert.Equal("Volley 6", all[0].Source);
    }

    [Fact]
    public void AllFourMaxed_GivesSixBonuses() =>
        Assert.Equal(6, For((ArcaneId.Volley, 6), (ArcaneId.Reach, 6), (ArcaneId.Attunement, 6), (ArcaneId.Alchemy, 6)).Count);

    [Theory]
    [InlineData(StatCalc.PercentAdd, 0f, 30f)]
    [InlineData(StatCalc.PercentAdd, 1f, 30f)]
    [InlineData(StatCalc.Flat, 20f, 6f)]
    [InlineData(StatCalc.Flat, -10f, -3f)]
    public void PercentBonus_ModifierValue(StatCalc calc, float baseValue, float expected) =>
        Assert.Equal(expected, RunBonuses.ModifierValue(ThirtyPercent, calc, baseValue)!.Value, 3);

    [Theory]
    [InlineData(StatCalc.Flat, 0f)]
    [InlineData(StatCalc.Flat, float.NaN)]
    [InlineData(StatCalc.Flat, float.PositiveInfinity)]
    [InlineData(StatCalc.PercentAddLogarithmic, 1f)]
    [InlineData(StatCalc.Unknown, 1f)]
    public void PercentBonus_UnsafeCases_AreSkipped(StatCalc calc, float baseValue) =>
        Assert.Null(RunBonuses.ModifierValue(ThirtyPercent, calc, baseValue));

    [Fact]
    public void FlatBonus_OnlyOnFlatStats()
    {
        var one = new StatBonus(RunBonuses.ProjectilesStat, 1f, false, BonusTarget.Character, "test");
        Assert.Equal(1f, RunBonuses.ModifierValue(one, StatCalc.Flat, 0f));
        Assert.Null(RunBonuses.ModifierValue(one, StatCalc.PercentAdd, 1f));
    }

    [Theory]
    [InlineData("PercentAdd", StatCalc.PercentAdd)]
    [InlineData("Flat", StatCalc.Flat)]
    [InlineData("PercentAddLogarithmic", StatCalc.PercentAddLogarithmic)]
    [InlineData("Unknown", StatCalc.Unknown)]
    [InlineData("SomethingNew", StatCalc.Unknown)]
    [InlineData(null, StatCalc.Unknown)]
    public void ParseCalc_ByTheGameName(string? name, StatCalc expected) => Assert.Equal(expected, RunBonuses.ParseCalc(name));
}
