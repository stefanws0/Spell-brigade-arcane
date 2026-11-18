namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class LoadRulesTests
{
    private static readonly IReadOnlyList<EnchantmentDef> Five = Catalog.Build();
    private static readonly IReadOnlyList<EnchantmentDef> Two = Catalog.Build(maxLevel: 2);

    private static SaveData Save(long? stamp, params int[] volley) =>
        new() { GameSaveClearedTicks = stamp, Paid = { ["Volley"] = volley.ToList() } };

    [Fact]
    public void NoStoredStamp_ValidCurrent_KeepsLevels_AndStampsTheSave()
    {
        var r = LoadRules.Decide(Five, Save(null, 10, 20), 500);
        Assert.False(r.WasReset);
        Assert.Equal(2, r.Progress.Level(ArcaneId.Volley));
        Assert.Equal(500, r.StampToWrite);
    }

    [Fact]
    public void StoredEqualsCurrent_IsNotAReset()
    {
        var r = LoadRules.Decide(Five, Save(500, 10), 500);
        Assert.False(r.WasReset);
        Assert.Equal(1, r.Progress.Level(ArcaneId.Volley));
        Assert.Equal(500, r.StampToWrite);
    }

    [Fact]
    public void StoredDiffersFromCurrent_ResetsWithoutGoldBack()
    {
        var r = LoadRules.Decide(Two, Save(400, 10, 20, 30), 500);
        Assert.True(r.WasReset);
        Assert.Equal(0, r.Progress.TotalLevels);
        Assert.Equal(0, r.ExcessGold);
        Assert.Equal(500, r.StampToWrite);
    }

    [Fact]
    public void UnknownCurrent_NeverResets_AndKeepsTheStoredStamp()
    {
        var r = LoadRules.Decide(Five, Save(400, 10), 0);
        Assert.False(r.WasReset);
        Assert.Equal(1, r.Progress.Level(ArcaneId.Volley));
        Assert.Equal(400, r.StampToWrite);
    }

    [Fact]
    public void UnknownCurrent_NothingStored_WritesNoStamp()
    {
        var r = LoadRules.Decide(Five, Save(null, 10), -5);
        Assert.False(r.WasReset);
        Assert.Null(r.StampToWrite);
    }

    [Fact]
    public void ExcessAboveMaxLevel_PassesThroughWhenNotReset()
    {
        var r = LoadRules.Decide(Two, Save(500, 10, 20, 30), 500);
        Assert.False(r.WasReset);
        Assert.Equal(30, r.ExcessGold);
        Assert.Equal(2, r.Progress.Level(ArcaneId.Volley));
        Assert.NotEmpty(r.Notes);
    }
}
