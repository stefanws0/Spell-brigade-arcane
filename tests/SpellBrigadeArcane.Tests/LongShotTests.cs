namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class LongShotTests
{
    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(4f, 1f)]
    [InlineData(10f, 1.1f)]
    [InlineData(16f, 1.2f)]
    [InlineData(100f, 1.2f)]
    public void Multiplier_GrowsLinearlyFromNearToFar(float distance, float expected) =>
        Assert.Equal(expected, LongShot.Multiplier(distance), 4);

    [Fact]
    public void Multiplier_TreatsNaNAsNoBonus() => Assert.Equal(1f, LongShot.Multiplier(float.NaN));

    [Fact]
    public void Multiplier_RejectsFarNotBeyondNear() =>
        Assert.Throws<ArgumentException>(() => LongShot.Multiplier(5f, near: 8f, far: 8f));

    [Theory]
    [InlineData(100f, 2f, 100f)]
    [InlineData(100f, 4f, 100f)]
    [InlineData(100f, 10f, 110f)]
    [InlineData(100f, 16f, 120f)]
    [InlineData(100f, 40f, 120f)]
    [InlineData(50f, 16f, 60f)]
    public void Apply_ScalesDamageByDistance(float amount, float distance, float expected) =>
        Assert.Equal(expected, LongShot.Apply(amount, distance), 3);

    [Theory]
    [InlineData(0f, 20f)]
    [InlineData(-5f, 20f)]
    [InlineData(float.NaN, 20f)]
    [InlineData(float.PositiveInfinity, 20f)]
    public void Apply_LeavesOddAmountsAlone(float amount, float distance)
    {
        var result = LongShot.Apply(amount, distance);
        Assert.True(result.Equals(amount));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-3f)]
    public void Apply_OddDistances_GiveNoBonus(float distance) =>
        Assert.Equal(100f, LongShot.Apply(100f, distance));

    [Fact]
    public void Apply_NeverOverflows() => Assert.Equal(float.MaxValue, LongShot.Apply(float.MaxValue, 20f));

    [Fact]
    public void Apply_NegativeInfinityDistance_GivesNoBonus() => Assert.Equal(100f, LongShot.Apply(100f, float.NegativeInfinity));
}
