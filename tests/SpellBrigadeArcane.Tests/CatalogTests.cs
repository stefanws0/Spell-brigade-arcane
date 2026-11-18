namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class CatalogTests
{
    [Fact]
    public void Build_HasTheFourEnchantmentsInRowOrder()
    {
        var ids = Catalog.Build().Select(d => d.Id).ToArray();
        Assert.Equal(new[] { ArcaneId.Volley, ArcaneId.Reach, ArcaneId.Attunement, ArcaneId.Alchemy }, ids);
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(1, 6f)]
    [InlineData(5, 30f)]
    [InlineData(9, 30f)]
    public void Volley_SplitsItsTotalEvenlyOverFiveLevels(int level, float expected) =>
        Assert.Equal(expected, Catalog.Get(Catalog.Build(), ArcaneId.Volley).PercentAt("Speed", level), 4);

    [Fact]
    public void PercentAt_UsesTheRealLevelCount() =>
        Assert.Equal(15f, Catalog.Get(Catalog.Build(maxLevel: 6), ArcaneId.Reach).PercentAt("Range", 3), 4);

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
    public void Constructor_RejectsZeroLevels() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EnchantmentDef(ArcaneId.Volley, "n", "d", "m", "t", 0, new StatGain("Speed", 30f)));
}
