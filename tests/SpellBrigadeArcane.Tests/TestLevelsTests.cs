namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class TestLevelsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_MeansNoOverride(string? text)
    {
        var levels = TestLevels.Parse(text, 6, out var problems);
        Assert.Empty(levels);
        Assert.Empty(problems);
    }

    [Fact]
    public void NamesAndLevels_AnyCaseAndSpacing()
    {
        var levels = TestLevels.Parse(" Volley=6, alchemy = 3 ,REACH=0", 6, out var problems);
        Assert.Empty(problems);
        Assert.Equal(3, levels.Count);
        Assert.Equal(6, levels[ArcaneId.Volley]);
        Assert.Equal(3, levels[ArcaneId.Alchemy]);
        Assert.Equal(0, levels[ArcaneId.Reach]);
    }

    [Theory]
    [InlineData("Foo=2")]
    [InlineData("1=2")]
    [InlineData("Volley")]
    [InlineData("Volley=x")]
    [InlineData("Volley=2.5")]
    [InlineData("=3")]
    public void BadParts_AreReportedAndSkipped(string text)
    {
        var levels = TestLevels.Parse(text, 6, out var problems);
        Assert.Empty(levels);
        Assert.Single(problems);
    }

    [Fact]
    public void OutOfRange_IsClampedAndReported()
    {
        var levels = TestLevels.Parse("Volley=9, Reach=-1", 6, out var problems);
        Assert.Equal(6, levels[ArcaneId.Volley]);
        Assert.Equal(0, levels[ArcaneId.Reach]);
        Assert.Equal(2, problems.Count);
    }

    [Fact]
    public void QuotedValue_IsAccepted()
    {
        var levels = TestLevels.Parse("\"Volley=6, Alchemy=3\"", 6, out var problems);
        Assert.Empty(problems);
        Assert.Equal(6, levels[ArcaneId.Volley]);
        Assert.Equal(3, levels[ArcaneId.Alchemy]);
    }

    [Fact]
    public void TrailingComma_IsFine()
    {
        var levels = TestLevels.Parse("Volley=6,", 6, out var problems);
        Assert.Empty(problems);
        Assert.Equal(6, levels[ArcaneId.Volley]);
    }

    [Theory]
    [InlineData("Volley=")]
    [InlineData("Volley=99999999999")]
    public void MissingOrHugeLevel_IsOneProblem(string text)
    {
        var levels = TestLevels.Parse(text, 6, out var problems);
        Assert.Empty(levels);
        Assert.Single(problems);
    }

    [Fact]
    public void OwnMaxLevel_Clamps()
    {
        var levels = TestLevels.Parse("Volley=5", 3, out var problems);
        Assert.Equal(3, levels[ArcaneId.Volley]);
        Assert.Single(problems);
    }

    [Fact]
    public void GoodPartsSurviveBadOnes()
    {
        var levels = TestLevels.Parse("Volley=6, Nope=1", 6, out var problems);
        Assert.Equal(6, levels[ArcaneId.Volley]);
        Assert.Single(problems);
    }
}
