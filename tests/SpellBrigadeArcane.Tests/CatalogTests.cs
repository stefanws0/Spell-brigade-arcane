namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class CatalogTests
{
    [Fact]
    public void Build_HasTheFourEnchantmentsInColumnOrder()
    {
        var ids = Catalog.Build().Select(d => d.Id).ToArray();
        Assert.Equal(new[] { ArcaneId.Volley, ArcaneId.Reach, ArcaneId.Attunement, ArcaneId.Alchemy }, ids);
    }

    [Fact]
    public void Levels_AreFiveStatLevelsPlusTheMastery()
    {
        var volley = Catalog.Get(Catalog.Build(), ArcaneId.Volley);
        Assert.Equal(6, volley.MaxLevel);
        Assert.Equal(5, volley.StatLevels);
        Assert.Equal(5, volley.MasteryRevealLevel);
        Assert.False(volley.HasMastery(5));
        Assert.True(volley.HasMastery(6));
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(1, 6f)]
    [InlineData(5, 30f)]
    [InlineData(6, 30f)]
    [InlineData(9, 30f)]
    public void Volley_SpreadsItsTotalOverTheFiveStatLevels(int level, float expected) =>
        Assert.Equal(expected, Catalog.Get(Catalog.Build(), ArcaneId.Volley).PercentAt("Speed", level), 4);

    [Fact]
    public void PercentAt_UsesTheRealLevelCount() =>
        Assert.Equal(15f, Catalog.Get(Catalog.Build(maxLevel: 7), ArcaneId.Reach).PercentAt("Range", 3), 4);

    [Fact]
    public void PercentAt_IsZeroForAStatTheEnchantmentDoesNotRaise() =>
        Assert.Equal(0f, Catalog.Get(Catalog.Build(), ArcaneId.Volley).PercentAt("Range", 5), 4);

    [Fact]
    public void Alchemy_RaisesPotionsAndDropChance()
    {
        var alchemy = Catalog.Get(Catalog.Build(), ArcaneId.Alchemy);
        Assert.Equal(10f, alchemy.PercentAt("HealthPotionAmountMultiplier", 1), 4);
        Assert.Equal(5f, alchemy.PercentAt("DropChance", 1), 4);
    }

    [Fact]
    public void Gains_HaveLabelsForTheDetailsPanel()
    {
        var all = Catalog.Build();
        Assert.Equal("Projectile Speed", Catalog.Get(all, ArcaneId.Volley).Gains[0].Label);
        Assert.Equal("Spell Range", Catalog.Get(all, ArcaneId.Reach).Gains[0].Label);
        Assert.Equal("Elemental Damage", Catalog.Get(all, ArcaneId.Attunement).Gains[0].Label);
        Assert.Equal(new[] { "Potion Healing", "Potion Drop Chance" }, Catalog.Get(all, ArcaneId.Alchemy).Gains.Select(g => g.Label));
    }

    [Fact]
    public void MasteryTexts_ShowTheMasteryNumbers()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");
        try
        {
            var all = Catalog.Build();
            Assert.Contains("<b>20% more Damage</b>", Catalog.Get(all, ArcaneId.Reach).MasteryText);
            Assert.Contains("<b>4%</b>", Catalog.Get(all, ArcaneId.Attunement).MasteryText);
            Assert.Contains("<b>30%</b>", Catalog.Get(all, ArcaneId.Alchemy).MasteryText);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Constructor_RejectsFewerThanTwoLevels() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EnchantmentDef(ArcaneId.Volley, "n", "d", "m", "t", 1, new StatGain("Speed", 30f, "Projectile Speed")));
}
