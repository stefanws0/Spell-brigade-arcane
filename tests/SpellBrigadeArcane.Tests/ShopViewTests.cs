namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class ShopViewTests
{
    private static ButtonView View(int level, bool unlocked = true, int gold = 1000, int price = 500) =>
        new(level, maxLevel: 6, masteryRevealLevel: 5, unlocked, gold, price);

    [Fact]
    public void Ok_WhenUnlockedNotMaxedAndAffordable()
    {
        var v = View(level: 2);
        Assert.Equal(BuyResult.Ok, v.Check);
        Assert.True(v.CanAfford);
        Assert.False(v.Maxed);
    }

    [Fact]
    public void Locked_BeatsEverythingElse()
    {
        var v = View(level: 6, unlocked: false, gold: 0);
        Assert.Equal(BuyResult.Locked, v.Check);
        Assert.False(v.CanAfford);
    }

    [Fact]
    public void Maxed_AtTheMasteryLevel()
    {
        var v = View(level: 6);
        Assert.True(v.Maxed);
        Assert.Equal(BuyResult.Maxed, v.Check);
        Assert.False(v.CanAfford);
    }

    [Theory]
    [InlineData(499, BuyResult.TooExpensive, false)]
    [InlineData(500, BuyResult.Ok, true)]
    public void TooExpensive_WhenGoldIsShort(int gold, BuyResult expected, bool canAfford)
    {
        var v = View(level: 0, gold: gold, price: 500);
        Assert.Equal(expected, v.Check);
        Assert.Equal(canAfford, v.CanAfford);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void MasteryRevealed_FromTheRevealLevel(int level, bool revealed) =>
        Assert.Equal(revealed, View(level).MasteryRevealed);

    [Fact]
    public void For_ReadsLevelsFromTheBooks()
    {
        var catalog = Catalog.Build();
        var progress = new Progress(catalog);
        progress.TryBuy(ArcaneId.Reach, 400);
        var v = ButtonView.For(Catalog.Get(catalog, ArcaneId.Reach), progress, unlocked: true, gold: 900, price: 450);
        Assert.Equal(1, v.Level);
        Assert.Equal(6, v.MaxLevel);
        Assert.Equal(450, v.Price);
        Assert.Equal(BuyResult.Ok, v.Check);
    }
}
