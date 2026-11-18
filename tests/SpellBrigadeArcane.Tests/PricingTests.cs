namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class PricingTests
{
    [Theory]
    [InlineData(100, 150, 225)]
    [InlineData(400, 600, 900)]
    [InlineData(3, 5, 8)]
    [InlineData(2, 3, 5)]
    public void NextRowStart_RepeatsTheRowIVToRowVStep(int startIV, int startV, int expected) =>
        Assert.Equal(expected, Pricing.NextRowStart(startIV, startV));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-5, 100)]
    public void NextRowStart_RejectsNonPositivePrices(int startIV, int startV) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.NextRowStart(startIV, startV));

    [Fact]
    public void NextRowStart_ThrowsWhenThePriceDoesNotFitAnInt() =>
        Assert.Throws<OverflowException>(() => Pricing.NextRowStart(1, 50_000));

    [Fact]
    public void NextRowStart_IsNeverBelowOne() => Assert.Equal(1, Pricing.NextRowStart(100, 1));
}
