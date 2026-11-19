namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class PrismTrackerTests
{
    [Fact]
    public void NoElements_AddsNothing()
    {
        var prism = new PrismTracker();
        Assert.Equal(0f, prism.Update(0));
        Assert.Equal(0f, prism.Applied);
    }

    [Fact]
    public void EachNewElement_AddsOneStep()
    {
        var prism = new PrismTracker();
        Assert.Equal(4f, prism.Update(1));
        Assert.Equal(0f, prism.Update(1));
        Assert.Equal(8f, prism.Update(3));
        Assert.Equal(12f, prism.Applied);
        Assert.Equal(3, prism.Elements);
    }

    [Fact]
    public void FewerElements_NeverTakesAnythingBack()
    {
        var prism = new PrismTracker();
        prism.Update(3);
        Assert.Equal(0f, prism.Update(2));
        Assert.Equal(12f, prism.Applied);
        Assert.Equal(3, prism.Elements);
    }

    [Fact]
    public void StopsAtFiveElements()
    {
        var prism = new PrismTracker();
        Assert.Equal(20f, prism.Update(7));
        Assert.Equal(0f, prism.Update(5));
        Assert.Equal(20f, prism.Applied);
        Assert.Equal(5, prism.Elements);
    }

    [Fact]
    public void OwnStepSize()
    {
        var prism = new PrismTracker(2.5f);
        Assert.Equal(5f, prism.Update(2));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void BadStep_Throws(float step) => Assert.Throws<System.ArgumentOutOfRangeException>(() => new PrismTracker(step));
}
