namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class PotionBeltTests
{
    [Fact]
    public void StoresAPotionPickedUpAtFullHealth()
    {
        var belt = new PotionBelt();
        Assert.True(belt.OnPotionCollected(healthWasFull: true, healAmount: 120f));
        Assert.Equal(1, belt.Stored);
    }

    [Fact]
    public void IgnoresAPotionPickedUpWhenHurt()
    {
        var belt = new PotionBelt();
        Assert.False(belt.OnPotionCollected(healthWasFull: false, healAmount: 120f));
        Assert.Equal(0, belt.Stored);
    }

    [Fact]
    public void HoldsOnlyOne()
    {
        var belt = new PotionBelt();
        belt.OnPotionCollected(true, 120f);
        Assert.False(belt.OnPotionCollected(true, 80f));
        Assert.Equal(1, belt.Stored);
    }

    [Fact]
    public void DrinksOnlyBelowThirtyPercent()
    {
        var belt = new PotionBelt();
        belt.OnPotionCollected(true, 120f);
        Assert.Null(belt.TryDrink(health: 300f, maxHealth: 1000f));
        Assert.Equal((float?)120f, belt.TryDrink(health: 299f, maxHealth: 1000f));
        Assert.Equal(0, belt.Stored);
        Assert.Null(belt.TryDrink(health: 100f, maxHealth: 1000f));
    }

    [Fact]
    public void ADeadWizardDoesNotDrink()
    {
        var belt = new PotionBelt();
        belt.OnPotionCollected(true, 120f);
        Assert.Null(belt.TryDrink(health: 0f, maxHealth: 1000f));
        Assert.Equal(1, belt.Stored);
    }

    [Fact]
    public void ClearEmptiesTheBelt()
    {
        var belt = new PotionBelt();
        belt.OnPotionCollected(true, 120f);
        belt.Clear();
        Assert.Equal(0, belt.Stored);
    }

    [Theory]
    [InlineData(0f, 1)]
    [InlineData(1f, 1)]
    [InlineData(0.3f, 0)]
    public void RejectsBadSettings(float threshold, int capacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PotionBelt(threshold, capacity));
}
