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
    public void Multiplier_RejectsFarNotBeyondNear() =>
        Assert.Throws<ArgumentException>(() => LongShot.Multiplier(5f, near: 8f, far: 8f));
}
