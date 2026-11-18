namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class ProgressTests
{
    private static readonly IReadOnlyList<EnchantmentDef> Five = Catalog.Build();
    private static readonly IReadOnlyList<EnchantmentDef> Two = Catalog.Build(maxLevel: 2);

    [Fact]
    public void NewProgress_HasNothingBought()
    {
        var p = new Progress(Five);
        Assert.All(Enum.GetValues<ArcaneId>(), id => Assert.Equal(0, p.Level(id)));
        Assert.Equal(0, p.TotalLevels);
        Assert.Equal(0, p.TotalPaid);
    }

    [Fact]
    public void TryBuy_RecordsLevelAndPrice()
    {
        var p = new Progress(Five);
        Assert.True(p.TryBuy(ArcaneId.Reach, 300));
        Assert.True(p.TryBuy(ArcaneId.Reach, 450));
        Assert.Equal(2, p.Level(ArcaneId.Reach));
        Assert.Equal(new[] { 300, 450 }, p.Paid(ArcaneId.Reach));
        Assert.Equal(750, p.TotalPaid);
        Assert.Equal(2, p.TotalLevels);
    }

    [Fact]
    public void TryBuy_StopsAtMaxLevel()
    {
        var p = new Progress(Two);
        p.TryBuy(ArcaneId.Volley, 10);
        p.TryBuy(ArcaneId.Volley, 20);
        Assert.True(p.IsMaxed(ArcaneId.Volley));
        Assert.False(p.TryBuy(ArcaneId.Volley, 30));
        Assert.Equal(30, p.TotalPaid);
    }

    [Fact]
    public void TryBuy_RejectsNegativePrice() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Progress(Five).TryBuy(ArcaneId.Volley, -1));

    [Fact]
    public void RefundLast_ReturnsWhatTheTopLevelCost()
    {
        var p = new Progress(Five);
        p.TryBuy(ArcaneId.Alchemy, 100);
        p.TryBuy(ArcaneId.Alchemy, 150);
        Assert.Equal(150, p.RefundLast(ArcaneId.Alchemy));
        Assert.Equal(1, p.Level(ArcaneId.Alchemy));
    }

    [Fact]
    public void RefundLast_AtLevelZero_ReturnsNothing() =>
        Assert.Equal(0, new Progress(Five).RefundLast(ArcaneId.Volley));

    [Fact]
    public void RefundAll_ReturnsEverythingAndResets()
    {
        var p = new Progress(Five);
        p.TryBuy(ArcaneId.Volley, 100);
        p.TryBuy(ArcaneId.Attunement, 250);
        Assert.Equal(350, p.RefundAll());
        Assert.Equal(0, p.TotalLevels);
    }

    [Theory]
    [InlineData(11, false, false)]
    [InlineData(12, false, true)]
    [InlineData(20, false, true)]
    [InlineData(0, true, true)]
    public void IsUnlocked_NeedsTwelveMasteriesUnlessDebug(int masteries, bool debugUnlock, bool expected) =>
        Assert.Equal(expected, Progress.IsUnlocked(masteries, Progress.UnlockMasteries, debugUnlock));

    [Fact]
    public void SaveRoundTrip_KeepsPaidAmounts()
    {
        var p = new Progress(Five);
        p.TryBuy(ArcaneId.Reach, 300);
        p.TryBuy(ArcaneId.Reach, 450);
        var back = Progress.FromSave(Five, p.ToSave(), out var excess, out var notes);
        Assert.Equal(new[] { 300, 450 }, back.Paid(ArcaneId.Reach));
        Assert.Equal(0, excess);
        Assert.Empty(notes);
    }

    [Fact]
    public void FromSave_IgnoresUnknownNames()
    {
        var data = new SaveData { Paid = { ["Fireball"] = new List<int> { 5 }, ["2"] = new List<int> { 7 } } };
        var p = Progress.FromSave(Five, data, out _, out var notes);
        Assert.Equal(0, p.TotalLevels);
        Assert.Equal(2, notes.Count);
    }

    [Fact]
    public void FromSave_DropsLevelsAboveMaxAndReturnsTheirGold()
    {
        var data = new SaveData { Paid = { ["Volley"] = new List<int> { 10, 20, 30 } } };
        var p = Progress.FromSave(Two, data, out var excess, out var notes);
        Assert.Equal(2, p.Level(ArcaneId.Volley));
        Assert.Equal(30, excess);
        Assert.Single(notes);
    }

    [Fact]
    public void FromSave_HandlesNegativePricesAndMissingLists()
    {
        var data = new SaveData { Paid = { ["Reach"] = new List<int> { -5 }, ["Alchemy"] = null! } };
        var p = Progress.FromSave(Five, data, out _, out _);
        Assert.Equal(new[] { 0 }, p.Paid(ArcaneId.Reach));
        Assert.Equal(0, p.Level(ArcaneId.Alchemy));
    }
}
